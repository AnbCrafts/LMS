"""
security_guardrails.py
Comprehensive Security, Adversarial Guardrails, and RBAC Edge-Case Interceptor
Protects against:
- Prompt injection & jailbreaks (Direct overrides, roleplay, Base64 evasion, system prompt leakage)
- Academic dishonesty & homework evasion
- Student privacy / FERPA violations (grade snooping)
- Buffer overflow / excessive input DOS attacks
- Non-grounded hallucination traps (relevance threshold gating)
- Instructor grading boundaries (score range 0-100, ownership checks)
"""

import re
import base64
from typing import Tuple, Optional, Dict, Any, List
import pyodbc


# ============================================================================
# 1. PROMPT INJECTION & JAILBREAK PATTERNS
# ============================================================================

INJECTION_PATTERNS = [
    r"ignore\s+(?:all\s+)?(?:previous|prior|above|rules|guidelines|instructions)",
    r"disregard\s+(?:all\s+)?(?:rules|guidelines|instructions)",
    r"give\s+(?:me\s+)?(?:all\s+)?answers",
    r"you\s+are\s+now\s+in\s+developer\s+mode",
    r"enter\s+(?:developer|unrestricted|god|dan)\s+mode",
    r"pretend\s+you\s+are\s+(?:an?\s+)?(?:evil|unrestricted|jailbroken|rogue)\s+(?:ai|assistant|model)",
    r"roleplay\s+as\s+(?:an?\s+)?(?:evil|unrestricted|dan|jailbroken)",
    r"repeat\s+everything\s+(?:written\s+)?above",
    r"print\s+(?:your\s+)?(?:entire\s+)?system\s+prompt",
    r"output\s+(?:your\s+)?(?:internal\s+)?instructions\s+verbatim",
    r"reveal\s+(?:the\s+)?system\s+prompt",
    r"bypass\s+(?:all\s+)?(?:content\s+filters|rules|safety)",
    r"act\s+as\s+(?:DAN|an\s+unfiltered\s+model)",
    r"what\s+is\s+written\s+above\s+['\"]?rules\s+of\s+engagement['\"]?"
]

# Patterns where students ask for other students' grades
PRIVACY_SNOOPING_PATTERNS = [
    r"(?:what|show|tell|check)\s+(?:marks|score|grade|assignment)\s+(?:did|for|of)\s+([a-zA-Z\s]+)(?:\s+get|\s+receive|\?)?",
    r"(?:marks|score|grade)\s+(?:of|for)\s+([a-zA-Z\s]+)",
    r"(?:did\s+)([a-zA-Z\s]+)\s+(?:pass|fail|submit|get)",
    r"(?:show|view|see)\s+(?:other\s+)?students?\s+(?:grades?|marks?|submissions?)"
]


def try_decode_base64(text: str) -> Optional[str]:
    """
    Attempts to decode a string if it looks like Base64 obfuscation.
    """
    cleaned = text.strip()
    if len(cleaned) >= 8 and len(cleaned) % 4 == 0 and re.match(r"^[A-Za-z0-9+/]+={0,2}$", cleaned):
        try:
            decoded_bytes = base64.b64decode(cleaned)
            decoded_text = decoded_bytes.decode("utf-8", errors="ignore")
            # If the decoded string contains printable ASCII text of reasonable length
            if len(decoded_text) >= 4 and any(c.isalpha() for c in decoded_text):
                return decoded_text
        except Exception:
            pass
    return None


def sanitize_and_check_query(
    query: str,
    user_role: str = "student"
) -> Tuple[bool, str, Optional[str]]:
    """
    Validates, sanitizes, and evaluates an incoming user query.
    Returns: (is_safe, sanitized_query, rejection_reason)
    """
    if not query or not query.strip():
        return False, "", "Query cannot be empty. Please enter a valid course question."

    raw = query.strip()

    # 1. Length & Buffer Overflow Defense
    if len(raw) > 2000:
        raw = raw[:2000]
        # Flag if repetitive spam
        words = raw.split()
        if len(words) > 300 and len(set(words[:50])) < 5:
            return False, "", "Excessive repetitive input detected. Please formulate a concise question."

    # 2. Base64 Evasion Check
    decoded = try_decode_base64(raw)
    if decoded:
        for pattern in INJECTION_PATTERNS:
            if re.search(pattern, decoded, re.IGNORECASE):
                return False, raw, (
                    "Security Guardrail Triggered: Obfuscated Base64 instruction override attempt detected. "
                    "I am your dedicated academic teaching assistant and operate strictly within educational safety guidelines."
                )

    target_to_inspect = f"{raw} {decoded}" if decoded else raw

    # 3. Prompt Injection & Jailbreak Detection
    for pattern in INJECTION_PATTERNS:
        if re.search(pattern, target_to_inspect, re.IGNORECASE):
            return False, raw, (
                "Security Guardrail Triggered: System prompt override or jailbreak attempt detected. "
                "I am your dedicated academic teaching assistant and operate strictly within educational safety guidelines."
            )

    # 4. Student FERPA & Privacy Snooping Check (Students asking about other students' grades)
    if user_role.lower() == "student":
        for pattern in PRIVACY_SNOOPING_PATTERNS:
            match = re.search(pattern, raw, re.IGNORECASE)
            if match:
                target_person = match.group(1).strip() if match.groups() and match.group(1) else "another student"
                # Exclude if querying about themselves generically
                if target_person.lower() not in ("i", "me", "my"):
                    return False, raw, (
                        f"Student Privacy Guardrail: In accordance with academic privacy policies (FERPA), "
                        f"I cannot disclose the grades, submissions, or academic records of {target_person}. "
                        "You may only review your own course progress and marks on your student dashboard."
                    )

    # 5. Direct Quiz Answer Letter Cheating Request
    if user_role.lower() == "student":
        quiz_opt_match = re.search(r"(?:what\s+is\s+(?:the\s+)?correct\s+option|which\s+option\s+is\s+correct|is\s+it\s+A,\s*B,\s*C|tell\s+me\s+the\s+answer)\s+(?:for|to)\s+question\s*(\d+)", raw, re.IGNORECASE)
        if quiz_opt_match:
            q_num = quiz_opt_match.group(1)
            return False, raw, (
                f"Socratic Integrity Guardrail: I cannot provide the direct answer or option letter for Question {q_num}. "
                "However, if you paste the concepts or code snippet from the question, I will gladly explain the underlying theoretical principles to help you solve it yourself!"
            )

    return True, raw, None


# ============================================================================
# 2. INSTRUCTOR & ADMIN VALIDATION GUARDRAILS
# ============================================================================

def validate_instructor_grade_input(
    marks: float,
    submission_id: Optional[int],
    reviewer_id: int,
    user_role: str = "instructor"
) -> Tuple[bool, str, Optional[Dict[str, Any]]]:
    """
    Strictly validates grade boundaries and instructor course ownership.
    Edge Cases Handled:
    - Marks < 0 or Marks > 100
    - Non-existent submission ID
    - Submission belonging to another instructor's course (unauthorized cross-grading)
    """
    if marks < 0 or marks > 100:
        return False, f"Invalid Marks: Score must be between 0 and 100. (Received: {marks})", None

    if not submission_id:
        return False, "Submission ID is required to record a grade. E.g., 'Submit grade 90 for submission 7'.", None

    # Connect to database and verify submission existence and ownership
    from instructor_tools import get_db_connection
    conn = get_db_connection()
    if not conn:
        return False, "Database connection unavailable to verify submission.", None

    try:
        cur = conn.cursor()
        cur.execute("""
            SELECT 
                s.SubmissionId,
                s.CourseId,
                c.Title AS CourseTitle,
                c.InstructorId,
                u.FullName AS StudentName,
                s.Status
            FROM LMS.StudentSubmissions s
            JOIN LMS.Courses c ON s.CourseId = c.CourseId
            JOIN LMS.Users u ON s.UserId = u.UserId
            WHERE s.SubmissionId = ?
        """, (submission_id,))
        row = cur.fetchone()

        if not row:
            return False, f"Submission #{submission_id} does not exist in the database.", None

        sub_id, course_id, course_title, instructor_id, student_name, status = row

        # Check Course Ownership: Only the course's instructor or an Admin can submit grades
        if user_role.lower() != "admin" and int(instructor_id) != int(reviewer_id):
            return False, (
                f"Unauthorized Grade Modification: Submission #{submission_id} belongs to '{course_title}' "
                f"(taught by Instructor #{instructor_id}). Only the assigned instructor or a platform administrator may record marks for this submission."
            ), None

        return True, "Valid", {
            "submission_id": sub_id,
            "course_id": course_id,
            "course_title": course_title,
            "student_name": student_name,
            "current_status": status
        }
    except Exception as ex:
        return False, f"Database verification error: {str(ex)}", None
    finally:
        conn.close()


def validate_instructor_course_access(
    instructor_id: int,
    course_id: Optional[int],
    user_role: str = "instructor"
) -> Tuple[bool, str]:
    """
    Validates whether an instructor owns the requested course for analytics/friction heatmaps.
    """
    if not course_id:
        return True, "All owned courses"

    if user_role.lower() == "admin":
        return True, "Admin access"

    from instructor_tools import get_db_connection
    conn = get_db_connection()
    if not conn:
        return True, "Bypassed (DB offline)"

    try:
        cur = conn.cursor()
        cur.execute("SELECT InstructorId, Title FROM LMS.Courses WHERE CourseId = ?", (course_id,))
        row = cur.fetchone()
        if not row:
            return False, f"Course #{course_id} does not exist."
        
        owner_id, title = row
        if int(owner_id) != int(instructor_id):
            return False, f"Access Denied: You are not the assigned instructor for Course #{course_id} ('{title}'). You may only review analytics for courses you teach."
        
        return True, f"Owned course: {title}"
    except Exception as ex:
        return True, f"Error: {ex}"
    finally:
        conn.close()
