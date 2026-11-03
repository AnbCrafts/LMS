"""
ingest.py
Runnable demonstration of Phase 1:
1. Creates realistic course documents (student lecture notes & instructor secret solution key).
2. Chunks and prepares documents with RBAC metadata.
3. Computes embeddings with Gemini and stores them in ChromaDB.
4. Executes test queries proving hard RBAC boundary defense.
"""

import os
from chunker import prepare_documents_for_indexing
from vector_store import add_documents_to_vector_db, query_vector_db


def create_sample_docs():
    """
    Creates sample course files under a local 'sample_docs' folder
    to test both regular course knowledge and confidential materials.
    """
    folder = os.path.join(os.path.dirname(__file__), "sample_docs")
    os.makedirs(folder, exist_ok=True)
    
    # 1. Student-facing Syllabus & Lecture Notes (Public to enrolled students)
    student_doc = os.path.join(folder, "cs101_lecture1_web_architecture.txt")
    with open(student_doc, "w", encoding="utf-8") as f:
        f.write("""
        CS101: Modern Web Application Architecture.
        Lecture 1: HTTP Protocols, REST Principles, and Stateless Communication.

        1. The HTTP Protocol:
        The Hypertext Transfer Protocol (HTTP) is an application-layer protocol for distributed, collaborative, hypermedia information systems.
        HTTP operates via a client-server architecture. The browser (client) initiates a TCP handshake, sends an HTTP request, and awaits the server response.

        2. Idempotency of Methods:
        An HTTP method is considered idempotent if making multiple identical requests has the same intended effect on the server as making a single request.
        - GET: Idempotent and safe. Must only retrieve data and never alter database state.
        - PUT: Idempotent. Replaces the target resource with the uploaded payload.
        - DELETE: Idempotent. Deleting an item once or multiple times leaves the resource in the deleted state.
        - POST: NON-idempotent. Submitting multiple POST requests can create duplicate database records or trigger repeated charges.

        3. Course Deadlines and Evaluation Policies:
        Assignment 1 (Building an MVC Controller) is officially due on Friday, November 14 at 11:59 PM IST.
        Submissions turned in after the deadline incur a late penalty of 10% per calendar day.
        The passing threshold for the overall course certification is 75% aggregate score across assignments and quizzes.
        """)

    # 2. Instructor-Only Confidential Solution Manual (Must NOT be retrievable by students)
    instructor_doc = os.path.join(folder, "cs101_midterm_solution_keys_confidential.txt")
    with open(instructor_doc, "w", encoding="utf-8") as f:
        f.write("""
        CONFIDENTIAL INSTRUCTOR DOCUMENT - DO NOT DISTRIBUTE TO STUDENTS.
        CS101 Midterm Examination Official Answer Keys & Rubric:

        Question 1: Explain why HTTP POST is not considered idempotent.
        Correct Answer Key: Because sequential POST requests typically generate new server-side resources or side effects (e.g. inserting multiple records with different auto-increment IDs).
        Grading Criteria: Award 5 points if idempotency definition is accurate; award 5 points for practical example.

        Question 2: Official Exam Verification Password and Master Passkey.
        Internal Instructor Verification Token: 'ALPHA-9942-SECURE-KEY'.
        Students are strictly forbidden from having access to this token.
        """)
        
    return student_doc, instructor_doc


if __name__ == "__main__":
    print("=" * 70)
    print(" LMS RAG PIPELINE: PHASE 1 INGESTION & RBAC VERIFICATION")
    print("=" * 70)

    # Step 1: Create sample course materials
    print("\n[Step 1] Creating sample course documents...")
    student_file, instructor_file = create_sample_docs()
    print(f"  Created student doc: {os.path.basename(student_file)}")
    print(f"  Created confidential doc: {os.path.basename(instructor_file)}")

    # Step 2: Semantic chunking with metadata
    print("\n[Step 2] Chunking documents and attaching metadata...")
    student_chunks = prepare_documents_for_indexing(
        file_path=student_file,
        course_id=1,
        visibility="student",
        doc_type="lecture_notes"
    )
    print(f"  Prepared {len(student_chunks)} chunk(s) with visibility='student'")

    instructor_chunks = prepare_documents_for_indexing(
        file_path=instructor_file,
        course_id=1,
        visibility="instructor_only",
        doc_type="solution_key"
    )
    print(f"  Prepared {len(instructor_chunks)} chunk(s) with visibility='instructor_only'")

    # Step 3: Embed & Store in ChromaDB
    print("\n[Step 3] Computing Gemini embeddings and indexing into ChromaDB...")
    add_documents_to_vector_db(student_chunks + instructor_chunks)

    # Step 4: Testing Student Query (Allowed Retrieval)
    print("\n" + "=" * 70)
    print(" RETRIEVAL TEST 1: Student asks about Assignment Deadline & Penalties")
    print("=" * 70)
    query_1 = "When is Assignment 1 due and what is the late submission penalty?"
    print(f"Query: \"{query_1}\"")
    print(f"Role: student (visibility must strictly equal 'student')")
    
    results_1 = query_vector_db(query_1, course_id=1, user_role="student", top_k=2)
    for i, res in enumerate(results_1, 1):
        print(f"\n  [Result #{i}] Similarity: {res['similarity_score']} | Source: {res['metadata']['source_file']} (p.{res['metadata']['page_number']})")
        print(f"  Excerpt: {res['content'].strip()[:200]}...")

    # Step 5: Testing Jailbreak / Confidential Leak (Blocked by Hard RBAC)
    print("\n" + "=" * 70)
    print(" RETRIEVAL TEST 2: Student tries to retrieve confidential exam keys")
    print("=" * 70)
    query_2 = "What is the secret internal instructor verification passkey for the exam?"
    print(f"Query: \"{query_2}\"")
    print(f"Role: student")
    
    results_2 = query_vector_db(query_2, course_id=1, user_role="student", top_k=2)
    print(f"\n  Total chunks returned: {len(results_2)}")
    for res in results_2:
        print(f"  Returned File: {res['metadata']['source_file']} | Visibility: {res['metadata']['visibility']}")
    
    # Assert security
    leaked = any(r['metadata']['visibility'] == 'instructor_only' for r in results_2)
    if not leaked:
        print("\n  [PASS] SECURITY RBAC VERIFIED: Student cannot physically retrieve instructor-only chunks!")
    else:
        print("\n  [FAIL] SECURITY BREACH: Instructor-only chunk was returned to student!")

    # Step 6: Testing Instructor Query (Allowed to retrieve confidential materials)
    print("\n" + "=" * 70)
    print(" RETRIEVAL TEST 3: Instructor queries the confidential rubric")
    print("=" * 70)
    print(f"Query: \"{query_2}\"")
    print(f"Role: instructor (has access to both student and instructor_only)")
    
    results_3 = query_vector_db(query_2, course_id=1, user_role="instructor", top_k=1)
    for res in results_3:
        print(f"\n  Instructor Retrieved: {res['metadata']['source_file']} | Visibility: {res['metadata']['visibility']}")
        print(f"  Content: {res['content'].strip()}")

    print("\n" + "=" * 70)
    print(" PHASE 1 SETUP & VERIFICATION COMPLETED SUCCESSFULLY!")
    print("=" * 70)
