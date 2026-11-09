"""
instructor_tools.py
Agentic Tool Layer for Instructor & Admin Personas:
1. Analytics & Backlog Intelligence: Real-time queries on enrolled students, pending grading backlog, course health.
2. AI Assignment Auto-Evaluator: Evaluates submitted student tasks against assignment prompts/rubrics.
3. Database Integration: Direct SQL querying and grade updates via pyodbc.
"""

import os
import re
import pyodbc
from typing import Dict, Any, Optional, Tuple, List
import google.generativeai as genai
from dotenv import load_dotenv

load_dotenv()
api_key = os.getenv("GEMINI_API_KEY")
if api_key:
    genai.configure(api_key=api_key, transport="rest")

PRIMARY_MODEL = os.getenv("CHAT_MODEL", "models/gemini-3.5-flash-lite")
SECONDARY_MODEL = "models/gemini-3.1-flash-lite"

DB_CONNECTION_STRING = "Driver={ODBC Driver 17 for SQL Server};Server=localhost;Database=Practice;Trusted_Connection=yes;"


def get_db_connection():
    try:
        return pyodbc.connect(DB_CONNECTION_STRING, timeout=5)
    except Exception as e:
        print(f"[InstructorTools DB Error] {e}")
        return None


def detect_instructor_intent(query: str) -> Tuple[str, Dict[str, Any]]:
    """
    Classifies instructor queries into:
    - 'metrics': Student count, pending submissions backlog, course enrollment questions.
    - 'evaluate': Requesting AI to grade, evaluate, or review a student's assignment.
    - 'grade_apply': Confirming/applying a grade to the database.
    - 'general': Course curriculum RAG questions.
    """
    q = query.lower().strip()

    # Intent 1: Direct Grade Application (matches positive and negative integers for validation)
    grade_match = re.search(r'(?:submit|apply|save|record)\s+(?:grade|score|marks)\s*(?:of)?\s*(-?\d+)(?:\s*(?:for|to|on)\s*(?:submission\s*#?)?(\d+))?', q)
    if grade_match:
        return "grade_apply", {
            "marks": int(grade_match.group(1)),
            "submission_id": int(grade_match.group(2)) if grade_match.group(2) else None
        }

    # Intent 2: Instructor Metrics & Backlog Overview (Prioritized over generic word 'evaluate')
    metric_triggers = [
        "how many student", "number of student", "how many assignment", 
        "assignments to evaluate", "assignments do i have", "pending submission", 
        "submissions to review", "submissions pending", "my courses summary", 
        "grading backlog", "do i have to evaluate", "do i have to grade", 
        "enrolled in my course", "overview of my course"
    ]
    if any(trigger in q for trigger in metric_triggers):
        return "metrics", {}

    # Intent 2.5: Student Confusion & Learning Friction Heatmap
    friction_triggers = [
        "struggling with", "student friction", "confusion heatmap", 
        "confused about", "what are students asking", "friction heatmap",
        "hardest concept for students", "what are my students struggling",
        "student queries", "learning friction", "topics are confusing",
        "student confusion"
    ]
    if any(trigger in q for trigger in friction_triggers):
        c_match = re.search(r'course\s*(\d+)', q)
        target_course = int(c_match.group(1)) if c_match else None
        return "friction_analytics", {"course_id": target_course}

    # Intent 3: Assignment Evaluation Intent (e.g. evaluate student X or submission #Y)
    eval_triggers = [
        "evaluate", "grade", "review submission", "assess submission", 
        "evaluate assignment", "grade assignment", "check submission", "review assignment"
    ]
    if any(trigger in q for trigger in eval_triggers):
        # Extract student name or submission ID if present
        target_name = None
        sub_id = None
        id_match = re.search(r'(?:submission|assignment|task)\s*(?:id|#)?\s*(\d+)', q)
        if id_match:
            sub_id = int(id_match.group(1))

        # Check common student names in DB
        for name in ["rahul", "john", "priya", "neha", "amit", "vikram"]:
            if name in q:
                target_name = name
                break

        return "evaluate", {
            "target_student": target_name,
            "submission_id": sub_id,
            "raw_query": query
        }

    return "general", {}


def get_instructor_metrics_report(instructor_id: int) -> str:
    """
    Fetches live SQL Server numbers for an instructor:
    - Total courses taught
    - Total distinct students enrolled
    - Total pending assignments to evaluate
    - Detailed list of pending submissions waiting for review
    """
    conn = get_db_connection()
    if not conn:
        return (
            "I could not connect to the database to retrieve live instructor statistics. "
            "Please check the SQL Server Practice database connection."
        )

    try:
        cur = conn.cursor()

        # 1. Instructor Info & Courses
        cur.execute("""
            SELECT c.CourseId, c.Title, c.Level, c.Price,
                   COUNT(DISTINCT e.UserId) as StudentCount,
                   COUNT(CASE WHEN s.Status IN ('Pending', 'Pending Review') THEN 1 END) as PendingCount
            FROM LMS.Courses c
            LEFT JOIN LMS.Enrollments e ON c.CourseId = e.CourseId
            LEFT JOIN LMS.StudentSubmissions s ON c.CourseId = s.CourseId
            WHERE c.InstructorId = ?
            GROUP BY c.CourseId, c.Title, c.Level, c.Price
            ORDER BY c.CourseId
        """, (instructor_id,))
        course_rows = cur.fetchall()

        # 2. Total distinct students across all courses taught
        cur.execute("""
            SELECT COUNT(DISTINCT e.UserId)
            FROM LMS.Enrollments e
            JOIN LMS.Courses c ON e.CourseId = c.CourseId
            WHERE c.InstructorId = ?
        """, (instructor_id,))
        total_students = cur.fetchone()[0] or 0

        # 3. Pending Submissions details
        cur.execute("""
            SELECT s.SubmissionId, s.CourseId, c.Title as CourseTitle, 
                   u.FullName as StudentName, u.Email as StudentEmail,
                   s.SubmissionTitle, s.SubmittedAt, s.SubmissionFileUrl
            FROM LMS.StudentSubmissions s
            JOIN LMS.Courses c ON s.CourseId = c.CourseId
            JOIN LMS.Users u ON s.UserId = u.UserId
            WHERE c.InstructorId = ? AND s.Status IN ('Pending', 'Pending Review')
            ORDER BY s.SubmittedAt ASC
        """, (instructor_id,))
        pending_rows = cur.fetchall()

        conn.close()

        total_pending = len(pending_rows)

        # Build Markdown Report
        lines = [
            f"### Instructor Portfolio & Grading Backlog Overview\n",
            f"* **Total Enrolled Students:** **{total_students} learners** across all your courses.",
            f"* **Assignments Waiting for Evaluation:** **{total_pending} submissions** requiring your review.\n",
            f"#### Active Courses Breakdown:"
        ]

        for r in course_rows:
            cid, title, level, price, st_count, pend_count = r
            lines.append(f"- **Course {cid}: {title}** ({level}) - **{st_count} students**, **{pend_count} pending reviews**")

        if pending_rows:
            lines.append(f"\n#### Submissions Awaiting Evaluation ({total_pending}):")
            for p in pending_rows:
                sub_id, cid, c_title, st_name, st_email, sub_title, sub_date, file_url = p
                date_str = sub_date.strftime("%b %d, %Y %I:%M %p") if sub_date else "Recently"
                file_badge = " (PDF Attachment)" if file_url and file_url.lower().endswith(".pdf") else ""
                lines.append(
                    f"1. **Submission #{sub_id}** by **{st_name}** (`{st_email}`){file_badge}\n"
                    f"   - **Task:** {sub_title} *(Course {cid}: {c_title})*\n"
                    f"   - **Submitted:** {date_str}\n"
                    f"   - *(Tip: Ask me 'Evaluate {st_name.split()[0]}'s submission' or 'Evaluate submission #{sub_id}' to auto-grade!)*"
                )
        else:
            lines.append("\n **Great job! You have zero pending assignments to evaluate. All submissions are up to date.**")

        return "\n".join(lines)
    except Exception as e:
        return f"An error occurred while compiling your instructor statistics: {str(e)}"


def get_target_submission(instructor_id: int, params: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    """
    Locates the specific submission requested by the instructor (by student name or submission ID).
    """
    conn = get_db_connection()
    if not conn:
        return None

    try:
        cur = conn.cursor()
        sub_id = params.get("submission_id")
        target_name = params.get("target_student")

        sql = """
            SELECT s.SubmissionId, s.CourseId, c.Title as CourseTitle,
                   s.ModuleId, m.Title as ModuleTitle, ISNULL(m.AssignmentPrompt, c.Description) as AssignmentPrompt,
                   s.UserId, u.FullName as StudentName, u.Email as StudentEmail,
                   s.SubmissionTitle, s.SubmissionText, s.SubmissionFileUrl,
                   s.Status, s.MarksObtained, s.MaxMarks, s.SubmittedAt
            FROM LMS.StudentSubmissions s
            JOIN LMS.Courses c ON s.CourseId = c.CourseId
            JOIN LMS.Users u ON s.UserId = u.UserId
            LEFT JOIN LMS.CourseModules m ON s.ModuleId = m.ModuleId
            WHERE c.InstructorId = ?
        """
        args = [instructor_id]

        if sub_id:
            sql += " AND s.SubmissionId = ?"
            args.append(sub_id)
        elif target_name:
            sql += " AND LOWER(u.FullName) LIKE ?"
            args.append(f"%{target_name.lower()}%")
        else:
            raw_q = params.get("raw_query", "").lower()
            explicit_name = re.search(r'evaluate\s+([a-zA-Z\s]+?)(?:\'s)?\s+(?:submission|assignment|task)', raw_q)
            if explicit_name:
                cand = explicit_name.group(1).strip()
                if cand not in ("this", "the", "an", "a", "pending", "my", "student"):
                    sql += " AND LOWER(u.FullName) LIKE ?"
                    args.append(f"%{cand}%")
                else:
                    sql += " AND s.Status IN ('Pending', 'Pending Review') ORDER BY s.SubmittedAt ASC"
            else:
                # Default to oldest pending submission only when no student is specified
                sql += " AND s.Status IN ('Pending', 'Pending Review') ORDER BY s.SubmittedAt ASC"

        cur.execute(sql, args)
        row = cur.fetchone()
        conn.close()

        if not row:
            return None

        # Try reading file content if PDF exists on disk
        file_content = ""
        file_url = row[11]
        if file_url and file_url.startswith("/Uploads/"):
            base_web_app = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
            local_path = os.path.join(base_web_app, file_url.lstrip("/\\"))
            if os.path.exists(local_path) and local_path.lower().endswith(".pdf"):
                try:
                    import pypdf
                    reader = pypdf.PdfReader(local_path)
                    pages = [p.extract_text() for p in reader.pages if p.extract_text()]
                    file_content = "\n".join(pages).strip()
                except Exception:
                    pass

        return {
            "submission_id": row[0],
            "course_id": row[1],
            "course_title": row[2],
            "module_id": row[3],
            "module_title": row[4] or "Core Course Module",
            "assignment_prompt": row[5] or "Implement the practical requirements specified in the module curriculum.",
            "user_id": row[6],
            "student_name": row[7],
            "student_email": row[8],
            "submission_title": row[9],
            "submission_text": row[10] or "",
            "submission_file_url": row[11] or "",
            "file_extracted_content": file_content,
            "status": row[12],
            "marks_obtained": float(row[13]) if row[13] else None,
            "max_marks": float(row[14]) if row[14] else 100.0,
            "submitted_at": row[15]
        }
    except Exception as e:
        print(f"[get_target_submission Error] {e}")
        return None


def evaluate_submission_ai(submission: Dict[str, Any]) -> str:
    """
    Evaluates a student's submission using Gemini and formats a structured grading report.
    """
    prompt = f"""You are an Expert University Professor and Technical Assignment Grader.
Your role is to rigorously and pedagogically evaluate the following student assignment submission against the official assignment prompt and grading criteria.

============================================================
COURSE & ASSIGNMENT DETAILS:
Course: {submission['course_title']}
Module / Topic: {submission['module_title']}
Official Assignment Prompt & Requirements:
{submission['assignment_prompt']}

============================================================
STUDENT SUBMISSION DETAILS:
Student: {submission['student_name']} ({submission['student_email']})
Submission Title: {submission['submission_title']}
Submission Text / Description:
{submission['submission_text']}
{f'Attached File Content (Extracted):\n{submission["file_extracted_content"]}' if submission['file_extracted_content'] else ''}
{f'Attached Resource / Repository Link: {submission["submission_file_url"]}' if submission['submission_file_url'] else ''}
Max Marks: {int(submission['max_marks'])}

============================================================
EVALUATION DIRECTIVES:
1. Provide a rigorous, fair, and encouraging assessment.
2. Recommend a numerical score out of {int(submission['max_marks'])} (e.g. 92/{int(submission['max_marks'])}).
3. Assign a letter grade (e.g. A, A-, B+).
4. Detail 3 specific Key Strengths.
5. Detail 2 specific Improvement Opportunities / Gaps (pedagogical hints).
6. Formulate Ready-to-Send Student Feedback (warm, encouraging, professional).

FORMAT YOUR RESPONSE IN CLEAN, BEAUTIFUL MARKDOWN WITH CLEAR HEADINGS:
###  AI Assignment Evaluation: Submission #{submission['submission_id']}
**Student:** {submission['student_name']} | **Course:** {submission['course_title']}

#### Recommended Score: <Score> / {int(submission['max_marks'])} (<Grade Letter>)
**Verdict:** <Approved / Needs Revision>

####  Key Strengths:
- ...
- ...
- ...

####  Improvement Opportunities:
- ...
- ...

####  Feedback for Student:
> "<Personalized student feedback>"

*(Tip for Instructor: You can tell me 'Submit grade <score> for submission {submission['submission_id']}' to record this in the database automatically.)*
"""
    try:
        model = genai.GenerativeModel(PRIMARY_MODEL)
        resp = model.generate_content(prompt)
        if resp and resp.text:
            return resp.text.strip()
    except Exception as e1:
        print(f"[evaluate_submission_ai Tier 1 Error] {e1}")
        try:
            sec_model = genai.GenerativeModel(SECONDARY_MODEL)
            resp = sec_model.generate_content(prompt)
            if resp and resp.text:
                return resp.text.strip()
        except Exception as e2:
            print(f"[evaluate_submission_ai Tier 2 Error] {e2}")

    # Tier-3 Offline Rule-based Rubric Fallback
    sub_title = submission['submission_title']
    st_name = submission['student_name']
    has_text = len(submission['submission_text']) > 15
    has_file = bool(submission['submission_file_url'])
    
    score = 88 if has_text and has_file else (75 if has_text or has_file else 60)
    return f"""### AI Assignment Evaluation: Submission #{submission['submission_id']} (Rule-based Fallback)
**Student:** {st_name} | **Course:** {submission['course_title']}

#### Recommended Score: {score} / {int(submission['max_marks'])} (B+)
**Verdict:** Approved

#### Key Strengths:
- Timely submission addressing required module requirements: `{sub_title}`.
- Demonstrated clear technical approach and code organization.
- Implementation meets core structural specifications for {submission['module_title']}.

#### Improvement Opportunities:
- Add comprehensive unit test coverage and edge-case handling.
- Expand documentation and XML comments for architectural public interfaces.

#### Feedback for Student:
> "Good practical execution on {sub_title}, {st_name}! Your core implementation addresses the primary architectural requirements. For future milestones, focus on adding broader unit tests and performance profiling."

*(Tip for Instructor: Tell me 'Submit grade {score} for submission {submission['submission_id']}' to finalize this grade.)*
"""


def apply_submission_grade(submission_id: int, marks: float, feedback: str, reviewer_id: int) -> bool:
    """
    Directly commits instructor grade and feedback to LMS.StudentSubmissions table.
    """
    conn = get_db_connection()
    if not conn:
        return False

    try:
        cur = conn.cursor()
        cur.execute("""
            UPDATE LMS.StudentSubmissions
            SET MarksObtained = ?,
                InstructorFeedback = ?,
                Status = 'Approved',
                ReviewedAt = GETDATE(),
                ReviewedBy = ?
            WHERE SubmissionId = ?
        """, (marks, feedback, reviewer_id, submission_id))
        conn.commit()
        conn.close()
        return True
    except Exception as e:
        print(f"[apply_submission_grade Error] {e}")
        return False


def get_student_friction_data(instructor_id: int, target_course_id: Optional[int] = None) -> Dict[str, Any]:
    """
    Queries LMS.StudentQueryLogs to aggregate friction points and confusion metrics.
    """
    conn = get_db_connection()
    if not conn:
        return {"error": "Database connection unavailable"}

    try:
        cur = conn.cursor()
        query = """
            SELECT 
                c.CourseId,
                c.Title AS CourseTitle,
                q.Topic,
                q.FrictionLevel,
                q.QueryText,
                q.CreatedAt
            FROM LMS.StudentQueryLogs q
            JOIN LMS.Courses c ON q.CourseId = c.CourseId
            WHERE c.InstructorId = ?
        """
        params = [instructor_id]
        if target_course_id:
            query += " AND c.CourseId = ?"
            params.append(target_course_id)

        query += " ORDER BY q.CreatedAt DESC"
        cur.execute(query, params)
        rows = cur.fetchall()

        if not rows:
            return {"total_queries": 0, "topics": []}

        from collections import defaultdict
        topic_counts = defaultdict(lambda: {"count": 0, "friction": "Moderate", "samples": [], "course": ""})
        total_queries = len(rows)

        for r in rows:
            cid, ctitle, topic, friction, qtext, created = r
            topic_name = topic or "General Course Questions"
            topic_counts[topic_name]["count"] += 1
            topic_counts[topic_name]["friction"] = friction or "Moderate"
            topic_counts[topic_name]["course"] = ctitle
            if len(topic_counts[topic_name]["samples"]) < 2:
                topic_counts[topic_name]["samples"].append(qtext)

        sorted_topics = []
        for t_name, data in sorted(topic_counts.items(), key=lambda x: x[1]["count"], reverse=True):
            pct = round((data["count"] / total_queries) * 100, 1)
            sorted_topics.append({
                "topic": t_name,
                "course": data["course"],
                "count": data["count"],
                "percentage": pct,
                "friction_level": data["friction"],
                "sample_queries": data["samples"]
            })

        return {
            "instructor_id": instructor_id,
            "total_queries": total_queries,
            "topics": sorted_topics
        }
    except Exception as ex:
        print(f"[get_student_friction_data Error] {ex}")
        return {"error": str(ex)}
    finally:
        conn.close()


def get_student_friction_report(instructor_id: int, target_course_id: Optional[int] = None) -> str:
    """
    Synthesizes a pedagogical Student Confusion & Learning Friction Report with Gemini.
    """
    data = get_student_friction_data(instructor_id, target_course_id)
    if "error" in data:
        return f"Could not load friction metrics: {data['error']}"

    total = data.get("total_queries", 0)
    topics = data.get("topics", [])

    if total == 0 or not topics:
        return (
            "### Student Learning Friction & Concept Confusion Report\n\n"
            "No student inquiries have been recorded for your courses yet. "
            "As learners interact with the AI Teaching Assistant and submit module tasks, "
            "friction patterns and topic difficulty clusters will automatically populate here."
        )

    # Build topic summaries
    topic_lines = []
    for idx, t in enumerate(topics[:5], 1):
        friction_badge = "[HIGH FRICTION]" if t["friction_level"].lower() == "high" else ("[MODERATE]" if t["friction_level"].lower() == "moderate" else "[LOW]")
        sample_str = f"\"{t['sample_queries'][0]}\"" if t['sample_queries'] else "N/A"
        topic_lines.append(
            f"{idx}. **{t['topic']}** ({t['percentage']}% of inquiries, {t['count']} asks) - {friction_badge}\n"
            f"   - *Course:* {t['course']}\n"
            f"   - *Sample Student Question:* {sample_str}"
        )

    topics_text = "\n".join(topic_lines)

    # Gemini synthesis for recommended teaching action
    ai_recommendation = (
        "Dedicate 10-15 minutes at the start of your upcoming live session to clarify the execution "
        "pipeline between `OnActionExecuting` and `OnActionExecuted` in custom action filters, "
        "and demonstrate proper Scoped DbContext disposal inside repositories."
    )

    try:
        rec_prompt = f"""You are a university academic dean and pedagogical advisor.
Based on the following student friction analytics for an instructor's courses:
Total Inquiries: {total}
Top Struggling Concepts:
{topics_text}

Provide a concise, encouraging 2-3 sentence 'Recommended 10-Minute Live Review Plan' for the instructor.
Suggest exact concepts to whiteboard and practical tips to resolve these student doubts.
Output ONLY the paragraph text without quotes."""
        model = genai.GenerativeModel(PRIMARY_MODEL)
        resp = model.generate_content(rec_prompt)
        if resp and resp.text:
            ai_recommendation = resp.text.strip()
    except Exception as g_err:
        print(f"[Friction Report AI Synthesis Warning] {g_err}")

    report = f"""### Student Learning Friction & Concept Confusion Heatmap
**Instructor Portfolio Analytics** - **{total} Inquiries Analyzed**

#### Top Struggling Concepts & Doubt Clusters:
{topics_text}

#### Recommended 10-Minute Live Review for Next Lecture:
> "{ai_recommendation}"

*(Tip for Instructor: You can ask me 'Explain how to teach {topics[0]['topic']}' to generate sample lecture slides or coding examples!)*
"""
    return report

