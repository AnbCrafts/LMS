"""
rag_engine.py
Phase 2 & Resilience: RAG Retrieval, Socratic Prompt Engineering, Model Cascading, and Extractive Fallbacks.
- Assembles retrieved context chunks with source metadata.
- Tiered Model Cascade: Primary (3.5-flash-lite) -> Secondary (3.1-flash-lite) -> Tertiary (Extractive Offline Notes).
- Applies strict Socratic Teaching Assistant guardrails for students (anti-cheating hints).
- Returns structured answers with clean, verifiable citations.
"""

import os
import time
import re
from typing import List, Dict, Any, Optional
import google.generativeai as genai
from google.api_core import exceptions
from dotenv import load_dotenv

from vector_store import query_vector_db

# Load environment configuration
load_dotenv()
api_key = os.getenv("GEMINI_API_KEY")
if not api_key:
    raise ValueError("GEMINI_API_KEY is not defined in .env")

# Use REST transport for fast, firewall-friendly connection
genai.configure(api_key=api_key, transport="rest")

PRIMARY_MODEL_NAME = os.getenv("CHAT_MODEL", "models/gemini-3.5-flash-lite")
SECONDARY_MODEL_NAME = "models/gemini-3.1-flash-lite"


# ============================================================================
# SYSTEM PROMPTS (PEDAGOGY & SECURITY GUARDRAILS)
# ============================================================================

STUDENT_SYSTEM_PROMPT = """You are the dedicated AI Teaching Assistant for this course on the LMS.
Your core mission is to help learners understand complex concepts deeply through guided discovery, NOT by doing their homework for them.

RULES OF ENGAGEMENT:
1. GROUNDED IN CONTEXT: Base your explanations strictly on the provided Course Material Excerpts below.
2. MISSING INFORMATION: If the answer is not present in the provided excerpts, politely state:
   "I cannot find this information in the course materials currently available. Please consult your instructor or check the course discussion forum."
3. STRICT ANTI-CHEATING POLICY (SOCRATIC PEDAGOGY):
   If the student asks you to solve an assignment, complete homework, write the complete code for their submission, or provide direct answers to test questions:
   - YOU MUST REFUSE TO PROVIDE THE FINAL CODE OR FULL SOLUTION.
   - Instead, explain the underlying theoretical concept or principle involved.
   - Provide a minimal, abstract, or metaphorical example (do NOT use the assignment's exact variables/parameters).
   - Ask a guiding question or give a conceptual hint that leads the student to solve it on their own.
   - Point them to the relevant lecture slide or page from the context.
4. CITATIONS:
   Whenever you reference a fact, policy, or technical explanation, explicitly cite the source using this bracketed format:
   [Source: <filename>, Page: <page_number>]
5. TONE: Warm, encouraging, academic, and structured. Use Markdown formatting (bolding, lists) for clarity.
"""

INSTRUCTOR_SYSTEM_PROMPT = """You are an advanced Instructional Design Assistant for course instructors on the LMS.
You have full access to all course materials, including lecture notes, syllabi, and confidential solution keys.

CAPABILITIES & DIRECTIVES:
1. ASSESSMENT GENERATION: When asked to draft quiz questions, provide:
   - Clear question prompt
   - 4 plausible options (A, B, C, D)
   - Correct answer identified
   - Pedagogical rationale citing course materials [Source: <filename>, Page: <page_number>]
2. RUBRIC & PROMPT DRAFTING: Help construct fair, structured evaluation rubrics and assignment instructions.
3. CONTENT AUDITING: Identify any discrepancies between lecture notes, textbooks, and assignment requirements.
4. TONE: Professional, concise, academic, and actionable.
"""


class RAGEngine:
    def __init__(self, model_name: str = PRIMARY_MODEL_NAME):
        self.model_name = model_name
        self.secondary_model_name = SECONDARY_MODEL_NAME
        self.client = genai.GenerativeModel(
            model_name=self.model_name,
            generation_config={
                "temperature": 0.2,       # Low temperature for factual grounding
                "top_p": 0.85,
                "max_output_tokens": 1024
            }
        )

    def _format_context(self, retrieved_chunks: List[Dict[str, Any]]) -> str:
        if not retrieved_chunks:
            return "No relevant course excerpts found for this topic."

        formatted_parts = []
        for idx, chunk in enumerate(retrieved_chunks, 1):
            meta = chunk["metadata"]
            source = meta.get("source_file", "unknown_source.txt")
            page = meta.get("page_number", 1)
            course_id = meta.get("course_id", "General")
            content = chunk["content"].strip()
            formatted_parts.append(
                f"[Document {idx} | Course {course_id} | Source: {source} | Page: {page}]\n{content}"
            )

        return "\n\n".join(formatted_parts)

    def _extract_citations(self, text: str, retrieved_chunks: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
        citation_pattern = r"\[Source:\s*([^,\]]+),\s*Page:\s*(\d+)\]"
        matches = re.findall(citation_pattern, text)

        unique_citations = {}
        for filename, page_str in matches:
            key = (filename.strip(), int(page_str))
            if key not in unique_citations:
                unique_citations[key] = {
                    "source_file": filename.strip(),
                    "page_number": int(page_str)
                }

        if unique_citations:
            return list(unique_citations.values())

        # Fallback to top retrieved sources
        fallback_citations = []
        seen = set()
        for chunk in retrieved_chunks:
            meta = chunk["metadata"]
            k = (meta.get("source_file"), meta.get("page_number", 1))
            if k not in seen:
                seen.add(k)
                fallback_citations.append({
                    "source_file": meta.get("source_file"),
                    "page_number": meta.get("page_number", 1)
                })
        return fallback_citations

    def answer_query(
        self,
        query: str,
        course_id: int = 1,
        enrolled_course_ids: Optional[List[int]] = None,
        user_role: str = "student",
        top_k: int = 3
    ) -> Dict[str, Any]:
        """
        Main Resilient RAG pipeline:
        1. Retrieve top-k relevant chunks (supports multi-course search).
        2. Construct system prompt and context block.
        3. Model Cascade: Primary -> Secondary -> Tertiary Extractive Notes.
        4. Return answer, parsed citations, model used, and audit metadata.
        """
        # Step 1: Vector Retrieval with hard RBAC filter & multi-course support
        retrieved_chunks = query_vector_db(
            query_text=query,
            course_id=course_id,
            enrolled_course_ids=enrolled_course_ids,
            user_role=user_role,
            top_k=top_k
        )

        context_block = self._format_context(retrieved_chunks)

        # Step 2: Choose Persona System Prompt
        system_prompt = INSTRUCTOR_SYSTEM_PROMPT if user_role.lower() in ("instructor", "admin") else STUDENT_SYSTEM_PROMPT

        # Step 3: Build Assembled Prompt
        assembled_prompt = (
            f"{system_prompt}\n\n"
            f"========================================\n"
            f"AVAILABLE COURSE MATERIAL EXCERPTS:\n"
            f"========================================\n"
            f"{context_block}\n\n"
            f"========================================\n"
            f"USER QUERY ({user_role.upper()}):\n"
            f"{query}\n"
            f"========================================\n"
            f"Please formulate your response now adhering strictly to all pedagogical and citation rules:"
        )

        # Step 4: Tiered Model Cascading & Extractive Safe Fallback
        answer_text = ""
        model_used = self.model_name

        # Cascade Tier 1: Primary Model (gemini-3.5-flash-lite)
        try:
            response = self.client.generate_content(assembled_prompt)
            if response and response.text:
                answer_text = response.text.strip()
                model_used = self.model_name
        except (exceptions.ResourceExhausted, Exception) as ex1:
            print(f"[Resilience Alert] Primary model ({self.model_name}) error/quota: {ex1}. Cascading to secondary model ({self.secondary_model_name})...")
            
            # Cascade Tier 2: Secondary Model (gemini-3.1-flash-lite)
            try:
                secondary_client = genai.GenerativeModel(self.secondary_model_name)
                response = secondary_client.generate_content(assembled_prompt)
                if response and response.text:
                    answer_text = response.text.strip()
                    model_used = self.secondary_model_name
            except Exception as ex2:
                print(f"[Resilience Alert] Secondary model also unavailable: {ex2}. Activating Tier-3 Zero-Token Extractive Fallback...")

        # Cascade Tier 3: Zero-Token Local Extractive Synthesis (Guarantees 100% Uptime)
        if not answer_text or answer_text.strip() == "":
            model_used = "offline-extractive-fallback"
            if retrieved_chunks:
                excerpts_text = ""
                for idx, c in enumerate(retrieved_chunks, 1):
                    meta = c["metadata"]
                    file_src = meta.get("source_file", "Lecture Notes")
                    page_num = meta.get("page_number", 1)
                    cid = meta.get("course_id", "General")
                    excerpts_text += f"\n\n**Verified Excerpt {idx}** [Source: `{file_src}`, Page {page_num}, Course {cid}]:\n> {c['content'][:450]}..."

                answer_text = (
                    "### Course Knowledge Reference (High-Capacity Safe Mode)\n\n"
                    "Our AI assistant is currently operating under peak traffic conditions. "
                    "To ensure uninterrupted learning, here are the exact verified excerpts from your curriculum materials addressing your question:\n"
                    f"{excerpts_text}\n\n"
                    "*(Tip: You can download the complete study guide PDF above or ask again in a moment for interactive explanation.)*"
                )
            else:
                answer_text = (
                    "I could not find relevant excerpts for this topic in your currently enrolled course materials. "
                    "Please check the course syllabus or consult your instructor on the discussion board."
                )

        # Step 5: Format Citations
        citations = self._extract_citations(answer_text, retrieved_chunks)

        return {
            "query": query,
            "answer": answer_text,
            "role": user_role,
            "course_id": course_id,
            "citations": citations,
            "model_used": model_used,
            "sources_count": len(retrieved_chunks),
            "sources_used": [
                {
                    "source_file": c["metadata"].get("source_file"),
                    "page_number": c["metadata"].get("page_number", 1),
                    "course_id": c["metadata"].get("course_id", 1),
                    "similarity": c.get("similarity_score", 0.0),
                    "visibility": c["metadata"].get("visibility")
                }
                for c in retrieved_chunks
            ]
        }
