"""
db_ingest.py
Directly connects to the local SQL Server 'Practice' database (LMS schema)
and indexes real course curriculum (CourseModules, Courses, and Quizzes)
into the persistent ChromaDB collection.
"""

import os
import pyodbc
from chunker import chunk_text
from vector_store import add_documents_to_vector_db

CONNECTION_STRING = (
    "Driver={ODBC Driver 17 for SQL Server};"
    "Server=.;"
    "Database=Practice;"
    "Trusted_Connection=yes;"
)

def ingest_course_database_materials():
    print("=" * 70)
    print(" CONNECTING TO SQL SERVER (Practice DB) FOR RAG INGESTION")
    print("=" * 70)
    
    conn = pyodbc.connect(CONNECTION_STRING)
    cursor = conn.cursor()

    all_chunks = []

    # 1. Ingest Course Overview / Objectives
    print("\n[Step 1] Fetching Course Master records...")
    cursor.execute("SELECT CourseId, Title, Description, Category, Level, Price FROM LMS.Courses;")
    courses = cursor.fetchall()
    
    for c in courses:
        course_id, title, desc, cat, level, price = c
        overview_text = f"""
Course Title: {title}
Category: {cat}
Level: {level}
Price: ${price}
Course Overview & Curriculum Goals:
{desc}
        """.strip()

        chunks = chunk_text(overview_text, chunk_size=500, chunk_overlap=80)
        for idx, text in enumerate(chunks):
            all_chunks.append({
                "text": text,
                "metadata": {
                    "course_id": int(course_id),
                    "visibility": "student",
                    "doc_type": "course_overview",
                    "source_file": f"Course_{course_id}_Overview.md",
                    "page_number": 1,
                    "chunk_index": int(idx)
                }
            })
    print(f"  Indexed overview for {len(courses)} course(s).")

    # 2. Ingest Detailed Course Modules & Assignments
    print("\n[Step 2] Fetching Course Modules & Lesson Markdown...")
    cursor.execute("""
        SELECT m.ModuleId, m.CourseId, m.ModuleNumber, m.Title, 
               m.Description, m.ContentMarkdown, m.AssignmentPrompt,
               c.Title AS CourseTitle
        FROM LMS.CourseModules m
        JOIN LMS.Courses c ON m.CourseId = c.CourseId
        ORDER BY m.CourseId, m.ModuleNumber;
    """)
    modules = cursor.fetchall()

    for m in modules:
        mod_id, course_id, mod_num, title, desc, content, assignment, course_title = m
        clean_desc = desc or ""
        clean_content = content or ""
        clean_assignment = assignment or "No practical assignment assigned for this module."

        full_module_text = f"""
Course: {course_title}
Module #{mod_num}: {title}
Summary: {clean_desc}

Lesson Content & Notes:
{clean_content}

Assignment & Practice Brief:
{clean_assignment}
        """.strip()

        chunks = chunk_text(full_module_text, chunk_size=600, chunk_overlap=100)
        safe_title = "".join(ch for ch in title if ch.isalnum() or ch in (" ", "_", "-"))[:30].strip().replace(" ", "_")
        
        for idx, text in enumerate(chunks):
            all_chunks.append({
                "text": text,
                "metadata": {
                    "course_id": int(course_id),
                    "module_id": int(mod_id),
                    "visibility": "student",
                    "doc_type": "module_lesson",
                    "source_file": f"Module_{mod_num}_{safe_title}.md",
                    "page_number": int(mod_num),
                    "chunk_index": int(idx)
                }
            })
    print(f"  Processed {len(modules)} module(s).")

    # 3. Ingest Quiz Questions (Student: practice reviews | Instructor: full keys)
    print("\n[Step 3] Fetching Quiz Questions & Answer Keys...")
    cursor.execute("""
        SELECT QuestionId, CourseId, LessonNumber, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectOption
        FROM LMS.QuizQuestions;
    """)
    quizzes = cursor.fetchall()

    for q in quizzes:
        q_id, course_id, lesson_num, q_text, opt_a, opt_b, opt_c, opt_d, correct_opt = q
        
        # Student visible practice topic
        student_quiz_text = f"""
Practice Assessment Topic (Lesson #{lesson_num}):
Question: {q_text}
Key Options:
- {opt_a}
- {opt_b}
- {opt_c}
- {opt_d}
Review your module notes and understand the core principles before submitting this quiz question.
        """.strip()
        all_chunks.append({
            "text": student_quiz_text,
            "metadata": {
                "course_id": int(course_id),
                "visibility": "student",
                "doc_type": "quiz_review",
                "source_file": f"Quiz_Lesson_{lesson_num}_Q{q_id}.md",
                "page_number": int(lesson_num or 1),
                "chunk_index": 0
            }
        })

        # Instructor-Only official answer key
        instructor_quiz_text = f"""
CONFIDENTIAL INSTRUCTOR QUIZ KEY (Question #{q_id}, Lesson #{lesson_num}):
Question: {q_text}
Options:
A) {opt_a}
B) {opt_b}
C) {opt_c}
D) {opt_d}
Official Correct Option: {correct_opt}
        """.strip()
        all_chunks.append({
            "text": instructor_quiz_text,
            "metadata": {
                "course_id": int(course_id),
                "visibility": "instructor_only",
                "doc_type": "quiz_solution_key",
                "source_file": f"Instructor_Confidential_Quiz_Key_Q{q_id}.md",
                "page_number": int(lesson_num or 1),
                "chunk_index": int(q_id)
            }
        })

    print(f"  Processed {len(quizzes)} quiz item(s).")

    conn.close()

    print(f"\n[Step 4] Embedding & Upserting {len(all_chunks)} chunks into ChromaDB...")
    add_documents_to_vector_db(all_chunks)
    print("\n" + "=" * 70)
    print(" ALL DATABASE COURSE CONTENT SUCCESSFULLY INDEXED INTO RAG VECTOR STORE!")
    print("=" * 70)


if __name__ == "__main__":
    ingest_course_database_materials()
