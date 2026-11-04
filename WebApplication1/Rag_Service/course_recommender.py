"""
course_recommender.py
AI-Powered Semantic Course Recommendation & Advisory Engine.
Matches a learner's freeform interests, career aspirations, or domain goals against available courses.
- Handles exact matches.
- If no exact match exists, intelligently discovers the closest related/foundational courses.
- Provides personalized pedagogical rationales and learning pathway guidance.
- Tiered Resilience: Primary (Gemini 3.5 Flash Lite) -> Secondary (Gemini 3.1 Flash Lite) -> Tertiary (Vector Cosine Fallback).
"""

import os
import json
import pyodbc
from typing import List, Dict, Any, Optional
import google.generativeai as genai
from dotenv import load_dotenv

from resilience import cosine_similarity
from vector_store import get_query_embedding

load_dotenv()
api_key = os.getenv("GEMINI_API_KEY")
if api_key:
    genai.configure(api_key=api_key, transport="rest")

PRIMARY_MODEL = os.getenv("CHAT_MODEL", "models/gemini-3.5-flash-lite")
SECONDARY_MODEL = "models/gemini-3.1-flash-lite"

# Default fallback catalog if database connection fails
FALLBACK_CATALOG = [
    {
        "course_id": 1,
        "title": "Modern React 18 & TypeScript Masterclass",
        "category": "Frontend",
        "level": "Beginner",
        "price": 49.99,
        "duration_weeks": 8,
        "instructor": "A. Sharma",
        "description": "Learn to build high-performance, accessible, and enterprise-scale single page applications with React 18, TypeScript, custom hooks, Redux Toolkit, and Vite.",
        "modules": ["Course Introduction & Setup", "State Primitives & Advanced Hooks", "Asynchronous REST & Server State", "Performance Profiling & Production Deployment"]
    },
    {
        "course_id": 2,
        "title": "Enterprise ASP.NET MVC & SQL Server Architecture",
        "category": "Backend",
        "level": "Intermediate",
        "price": 69.99,
        "duration_weeks": 10,
        "instructor": "A. Sharma",
        "description": "Master monolithic and layered enterprise architecture using ASP.NET MVC 5, ADO.NET, Repository Patterns, stored procedures, authentication filters, and clean code principles.",
        "modules": ["MVC Architectural Lifecycle & HTTP Pipeline", "Repository Pattern & SQL Server Stored Procedures", "Enterprise Security, CSRF Tokens & RBAC Filters", "Performance Profiling, Bundling & Index Tuning"]
    },
    {
        "course_id": 3,
        "title": "Fullstack Next.js & Node.js Microservices",
        "category": "Fullstack",
        "level": "Advanced",
        "price": 89.99,
        "duration_weeks": 12,
        "instructor": "A. Sharma",
        "description": "Build complete end-to-end fullstack platforms combining Next.js App Router, serverless APIs, PostgreSQL, Redis caching, and real-time WebSockets.",
        "modules": ["Next.js App Router & Server Components", "Microservice Decomposition & Node.js APIs", "Real-Time WebSockets & Event Streaming", "Production Deployments & Edge Caching"]
    },
    {
        "course_id": 4,
        "title": "Docker, Kubernetes & Cloud CI/CD Pipelines",
        "category": "DevOps",
        "level": "Intermediate",
        "price": 59.99,
        "duration_weeks": 6,
        "instructor": "Dr. Vikram Sen",
        "description": "Containerize modern web applications, deploy microservices on Kubernetes clusters, automate test runs with GitHub Actions, and master zero-downtime rolling releases.",
        "modules": ["Docker Containerization & Image Optimization", "Kubernetes Pods, Services & Ingress", "CI/CD Pipelines with GitHub Actions", "Monitoring, Logging & Rollback Strategies"]
    },
    {
        "course_id": 5,
        "title": "Full Stack Web Development",
        "category": "Fullstack",
        "level": "Intermediate",
        "price": 99.99,
        "duration_weeks": 18,
        "instructor": "Neha Kapoor",
        "description": "Comprehensive full stack software development bootcamp with 5 minor projects and 3 industry-grade capstone projects covering frontend, backend, databases, and deployment.",
        "modules": ["Modern HTML5, CSS3 & Responsive UI", "JavaScript ES6+ & Component Architecture", "Backend REST APIs & Databases", "Capstone Industry Projects & Portfolio Prep"]
    },
    {
        "course_id": 6,
        "title": "Multi-Cloud Architecture on AWS and GCP",
        "category": "Cloud Architecture",
        "level": "Advanced",
        "price": 89.99,
        "duration_weeks": 8,
        "instructor": "Dr. Vikram Sen",
        "description": "Design resilient, scalable multi-region microservices on AWS and GCP with Terraform, infrastructure as code, VPC peering, and enterprise compliance.",
        "modules": ["Multi-Cloud Networking, VPC Peering & Transit Gateways", "Infrastructure as Code with Terraform", "High Availability & Multi-Region Disaster Recovery", "Cost Governance & Security Auditing"]
    }
]


class CourseRecommender:
    def __init__(self):
        self.catalog = self._load_catalog()
        self.course_embeddings: Dict[int, List[float]] = {}
        self._precompute_embeddings()

    def _load_catalog(self) -> List[Dict[str, Any]]:
        """Loads live courses and modules from SQL Server DB, falls back to static catalog if DB is unreachable."""
        try:
            conn = pyodbc.connect(
                "Driver={ODBC Driver 17 for SQL Server};Server=localhost;Database=Practice;Trusted_Connection=yes;",
                timeout=5
            )
            cur = conn.cursor()
            cur.execute("""
                SELECT c.CourseId, c.Title, c.Category, c.Level, c.Price, c.Description, 
                       ISNULL(u.FullName, c.InstructorName) AS InstructorName, c.DurationWeeks
                FROM LMS.Courses c
                LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                ORDER BY c.CourseId
            """)
            courses_map = {}
            for row in cur.fetchall():
                cid = row[0]
                courses_map[cid] = {
                    "course_id": cid,
                    "title": row[1],
                    "category": row[2] or "General",
                    "level": row[3] or "All",
                    "price": float(row[4] or 0),
                    "description": row[5] or "",
                    "instructor": row[6] or "Verified Instructor",
                    "duration_weeks": int(row[7] or 8),
                    "modules": []
                }

            # Fetch module titles
            cur.execute("SELECT CourseId, Title FROM LMS.CourseModules ORDER BY CourseId, ModuleNumber")
            for mrow in cur.fetchall():
                cid = mrow[0]
                if cid in courses_map and mrow[1]:
                    courses_map[cid]["modules"].append(mrow[1])

            conn.close()
            if courses_map:
                return list(courses_map.values())
        except Exception as e:
            print(f"[CourseRecommender] DB load exception: {e}. Utilizing fallback catalog.")
        
        return FALLBACK_CATALOG

    def _build_profile_text(self, course: Dict[str, Any]) -> str:
        mods_str = ", ".join(course.get("modules", []))
        return (
            f"Course Title: {course['title']}\n"
            f"Category: {course['category']}\n"
            f"Target Skill Level: {course['level']}\n"
            f"Description: {course['description']}\n"
            f"Curriculum Topics: {mods_str}"
        )

    def _precompute_embeddings(self):
        """Precomputes vector representations for all catalog courses."""
        print(f"[CourseRecommender] Pre-computing semantic embeddings for {len(self.catalog)} courses...")
        for c in self.catalog:
            cid = c["course_id"]
            text = self._build_profile_text(c)
            try:
                emb = get_query_embedding(text)
                self.course_embeddings[cid] = emb
            except Exception as e:
                print(f"[CourseRecommender] Failed embedding for course {cid}: {e}")

    def recommend(
        self,
        interests: str,
        user_level: Optional[str] = "All",
        top_k: int = 3
    ) -> Dict[str, Any]:
        """
        Finds the most relevant courses for the learner's expressed interests.
        1. Cosine similarity over catalog embeddings.
        2. Gemini LLM ranking + personalized rationale & pathway synthesis.
        3. Cascades gracefully to Tier-2/Tier-3 offline fallback if LLM is unavailable.
        """
        if not interests or not interests.strip():
            return {
                "success": False,
                "message": "Please describe your learning interests or goals.",
                "recommendations": []
            }

        # Step 1: Semantic Vector Ranking across all catalog courses
        q_vec = None
        scored_courses = []
        try:
            q_vec = get_query_embedding(interests)
            for c in self.catalog:
                cid = c["course_id"]
                emb = self.course_embeddings.get(cid)
                if emb:
                    sim = cosine_similarity(q_vec, emb)
                else:
                    sim = 0.5
                scored_courses.append((sim, c))
            scored_courses.sort(key=lambda x: x[0], reverse=True)
        except Exception as e:
            print(f"[CourseRecommender] Vector scoring fallback: {e}")
            scored_courses = [(0.7, c) for c in self.catalog]

        candidates = [c for _, c in scored_courses[:min(5, len(scored_courses))]]

        # Step 2: Build LLM Prompt for Socratic Matching & Rationale
        candidates_catalog_str = ""
        for idx, c in enumerate(candidates, 1):
            candidates_catalog_str += (
                f"\n[Option {idx}] CourseId: {c['course_id']} | Title: '{c['title']}' | "
                f"Category: {c['category']} | Level: {c['level']} | Price: ${c['price']}\n"
                f"Description: {c['description']}\n"
                f"Modules: {', '.join(c.get('modules', []))}\n"
            )

        prompt = f"""You are the Chief Academic Advisor and Course Recommender for EduPulse LMS.
A learner has described their fields of interest, career goals, or technical aspirations:
\"{interests.strip()}\"
Learner Skill Level Preference: {user_level or 'Any'}

We have the following available courses in our catalog:
{candidates_catalog_str}

YOUR TASK:
1. Evaluate how well each course satisfies or connects to the learner's goal.
2. If an EXACT or DIRECT match exists in the catalog:
   - Rank it at the top.
   - Assign a match_score between 88 and 99.
   - Set match_label to 'Direct Match' or 'Best Fit'.
3. If NO exact course exists (e.g. learner asked for Game Dev, Cyber Security, Blockchain, Mobile Apps, or specialized niche):
   - DO NOT hallucinate nonexistent courses.
   - Recommend the CLOSEST RELATED or FOUNDATIONAL courses available in our catalog.
   - Clearly explain the connection (e.g., how learning backend architecture or web sockets prepares them for multiplayer games or trading systems).
   - Assign a match_score between 65 and 85.
   - Set match_label to 'Foundational Pathway' or 'Related Core Skill'.
4. For each recommended course, provide a 2-sentence pedagogical rationale directly referencing their interest and explaining why this course will advance their objective.
5. Provide a 1-sentence 'pathway_tip' providing actionable advice on how to start.

OUTPUT FORMAT:
Return ONLY a valid JSON object matching this exact structure:
{{
  "has_exact_match": true,
  "pathway_tip": "Start with Course X to build foundation, then advance to Course Y.",
  "recommendations": [
    {{
      "course_id": 1,
      "match_score": 95,
      "match_label": "Direct Match",
      "rationale": "This course directly addresses your goal by..."
    }}
  ]
}}
Limit recommendations to at most {top_k} courses.
"""

        # Step 3: Tiered Model Cascade
        raw_json_str = ""
        model_used = PRIMARY_MODEL

        # Cascade Tier 1: Primary Model (3.5-flash-lite)
        try:
            model = genai.GenerativeModel(PRIMARY_MODEL)
            response = model.generate_content(prompt)
            if response and response.text:
                raw_json_str = response.text.strip()
        except Exception as e1:
            print(f"[CourseRecommender Alert] Tier-1 error: {e1}. Cascading to Tier-2 ({SECONDARY_MODEL})...")
            # Cascade Tier 2: Secondary Model (3.1-flash-lite)
            try:
                sec_model = genai.GenerativeModel(SECONDARY_MODEL)
                response = sec_model.generate_content(prompt)
                if response and response.text:
                    raw_json_str = response.text.strip()
                    model_used = SECONDARY_MODEL
            except Exception as e2:
                print(f"[CourseRecommender Alert] Tier-2 error: {e2}. Falling back to Tier-3 vector extractive synthesis.")

        # Parse LLM response
        if raw_json_str:
            try:
                clean_str = raw_json_str
                if clean_str.startswith("```json"):
                    clean_str = clean_str[7:]
                elif clean_str.startswith("```"):
                    clean_str = clean_str[3:]
                if clean_str.endswith("```"):
                    clean_str = clean_str[:-3]
                
                parsed = json.loads(clean_str.strip())
                
                # Enrich recommendation items with full course details from catalog
                enriched_recs = []
                for item in parsed.get("recommendations", []):
                    target_cid = item.get("course_id")
                    course_obj = next((c for c in self.catalog if c["course_id"] == target_cid), None)
                    if course_obj:
                        enriched_recs.append({
                            "course_id": target_cid,
                            "title": course_obj["title"],
                            "category": course_obj["category"],
                            "level": course_obj["level"],
                            "price": course_obj["price"],
                            "instructor": course_obj["instructor"],
                            "duration_weeks": course_obj["duration_weeks"],
                            "description": course_obj["description"],
                            "match_score": item.get("match_score", 85),
                            "match_label": item.get("match_label", "Recommended"),
                            "rationale": item.get("rationale", "")
                        })

                return {
                    "success": True,
                    "query_interests": interests,
                    "has_exact_match": parsed.get("has_exact_match", True),
                    "pathway_tip": parsed.get("pathway_tip", "Follow this sequence to maximize your practical learning."),
                    "model_used": model_used,
                    "recommendations": enriched_recs
                }
            except Exception as parse_err:
                print(f"[CourseRecommender] JSON parsing failed: {parse_err}")

        # Cascade Tier 3: Zero-Token Local Vector Fallback
        fallback_recs = []
        for sim, c in scored_courses[:top_k]:
            pct = int(min(98, max(60, sim * 100)))
            label = "Direct Match" if pct >= 82 else "Related Core Skill"
            fallback_recs.append({
                "course_id": c["course_id"],
                "title": c["title"],
                "category": c["category"],
                "level": c["level"],
                "price": c["price"],
                "instructor": c["instructor"],
                "duration_weeks": c["duration_weeks"],
                "description": c["description"],
                "match_score": pct,
                "match_label": label,
                "rationale": f"Selected based on semantic affinity with your interest in '{interests}'. This curriculum covers foundational {c['category']} practices."
            })

        return {
            "success": True,
            "query_interests": interests,
            "has_exact_match": any(r["match_score"] >= 80 for r in fallback_recs),
            "pathway_tip": "Explore these top-ranked courses to acquire relevant industry-tested engineering skills.",
            "model_used": "offline-vector-fallback",
            "recommendations": fallback_recs
        }


# Global Singleton
recommender = CourseRecommender()
