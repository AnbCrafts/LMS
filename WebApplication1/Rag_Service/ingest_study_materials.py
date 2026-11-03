"""
ingest_study_materials.py
Indexes all PDF study materials in WebApplication1/Uploads/CourseMaterials/ into ChromaDB.
"""

import os
import re
import glob
from dotenv import load_dotenv

from chunker import prepare_documents_for_indexing
from vector_store import add_documents_to_vector_db

load_dotenv()

MATERIALS_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Uploads", "CourseMaterials"))

def ingest_all_study_materials():
    pdf_files = glob.glob(os.path.join(MATERIALS_DIR, "*.pdf"))
    if not pdf_files:
        print("No PDF files found in:", MATERIALS_DIR)
        return

    print(f"Found {len(pdf_files)} PDF study guide(s) to index into ChromaDB...")
    all_chunks = []

    for pdf_path in pdf_files:
        filename = os.path.basename(pdf_path)
        m = re.search(r"Course(\d+)", filename, re.IGNORECASE)
        course_id = int(m.group(1)) if m else 1

        try:
            chunks = prepare_documents_for_indexing(
                file_path=pdf_path,
                course_id=course_id,
                visibility="student",
                doc_type="lecture_notes"
            )
            all_chunks.extend(chunks)
            print(f"  Processed {filename} -> {len(chunks)} chunk(s) (Course {course_id})")
        except Exception as ex:
            print(f"  Error reading {filename}: {ex}")

    print(f"\nUpserting {len(all_chunks)} chunks into ChromaDB...")
    add_documents_to_vector_db(all_chunks)
    print("PDF Study Materials successfully indexed into ChromaDB!")

if __name__ == "__main__":
    ingest_all_study_materials()
