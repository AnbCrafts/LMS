"""
test_filters.py
Unit & CI verification test for ChromaDB Role-Based Access Control (RBAC) metadata filters.
Designed to run both locally and in CI/CD pipelines (e.g., GitHub Actions) safely.
"""

import os
import sys

# Ensure vector_store can be imported
from vector_store import collection, EMBEDDING_MODEL

# 1. Obtain query embedding safely (mock fallback for CI environments with dummy keys)
def get_safe_embedding(text: str) -> list[float]:
    api_key = os.getenv("GEMINI_API_KEY", "")
    if api_key and api_key != "dummy_test_key_for_ci" and not api_key.startswith("dummy"):
        try:
            import google.generativeai as genai
            res = genai.embed_content(
                model=EMBEDDING_MODEL,
                content=text,
                task_type="retrieval_query"
            )
            return res["embedding"]
        except Exception as ex:
            print(f"[Notice] Live Gemini embedding call failed ({ex}). Falling back to synthetic vector for filter test.")
    
    # 768-dimensional deterministic mock vector for CI/CD test
    return [0.05] * 768

# 2. Ensure test documents exist in ChromaDB collection (seeding if empty, e.g. fresh CI clone)
if collection.count() == 0:
    print("[CI Setup] ChromaDB collection is empty. Seeding deterministic test documents for RBAC filter verification...")
    dummy_vec = [0.05] * 768
    collection.add(
        ids=["ci_doc_c1_student", "ci_doc_c2_student", "ci_doc_c2_instructor", "ci_doc_c3_student"],
        embeddings=[dummy_vec, dummy_vec, dummy_vec, dummy_vec],
        documents=[
            "Course 1 student guide: React 18 Concurrent Features.",
            "Course 2 student guide: ASP.NET MVC 5 Architecture and HTTP GET idempotency.",
            "Course 2 instructor secret: Midterm Exam Solution Manual and Grading Key.",
            "Course 3 student guide: Python Data Science with Pandas."
        ],
        metadatas=[
            {"course_id": 1, "module_id": 1, "visibility": "student", "source_file": "React18_Guide.pdf"},
            {"course_id": 2, "module_id": 1, "visibility": "student", "source_file": "MVC5_Arch.pdf"},
            {"course_id": 2, "module_id": 1, "visibility": "instructor_only", "source_file": "MVC5_Midterm_Solutions.pdf"},
            {"course_id": 3, "module_id": 1, "visibility": "student", "source_file": "Python_DS.pdf"}
        ]
    )
    print(f"[CI Setup] Seeded {collection.count()} test documents.")

# 3. Generate query vector
q_vec = get_safe_embedding("What is idempotency?")

# ------------------------------------------------------------------------------
# Test 1: Student Multi-Course Scoping ($in [1, 2]) & Visibility Exclusion
# ------------------------------------------------------------------------------
res1 = collection.query(
    query_embeddings=[q_vec],
    n_results=5,
    where={"$and": [{"course_id": {"$in": [1, 2]}}, {"visibility": {"$eq": "student"}}]}
)

matched_metadatas_1 = res1.get("metadatas", [[]])[0]
print(f"Test 1 ($in [1, 2] student): Found {len(matched_metadatas_1)} docs.")

for m in matched_metadatas_1:
    print(f"  Course: {m['course_id']} | Visibility: {m['visibility']} | Source: {m['source_file']}")
    assert m["course_id"] in [1, 2], f"Security Breach: Course {m['course_id']} leaked!"
    assert m["visibility"] == "student", "Security Breach: Instructor-only document leaked to student!"

# ------------------------------------------------------------------------------
# Test 2: Global Search for Instructor (No Student Visibility Restriction)
# ------------------------------------------------------------------------------
res2 = collection.query(
    query_embeddings=[q_vec],
    n_results=5
)

matched_metadatas_2 = res2.get("metadatas", [[]])[0]
print(f"Test 2 (global / instructor): Found {len(matched_metadatas_2)} docs.")
for m in matched_metadatas_2:
    print(f"  Course: {m['course_id']} | Source: {m['source_file']}")

# ------------------------------------------------------------------------------
# Test 3: Course 1 Strict Isolation (Verify Course 2 & 3 do not leak)
# ------------------------------------------------------------------------------
res3 = collection.query(
    query_embeddings=[q_vec],
    n_results=5,
    where={"$and": [{"course_id": {"$eq": 1}}, {"visibility": {"$eq": "student"}}]}
)
matched_metadatas_3 = res3.get("metadatas", [[]])[0]
print(f"Test 3 (Course 1 strict isolation): Found {len(matched_metadatas_3)} docs.")
for m in matched_metadatas_3:
    assert m["course_id"] == 1, f"Isolation failure: Course {m['course_id']} found in Course 1 scope!"

print("\n========================================================")
print(" ALL RBAC METADATA FILTER TESTS PASSED SUCCESSFULLY! ")
print("========================================================")
