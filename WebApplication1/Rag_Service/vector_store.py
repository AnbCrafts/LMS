"""
vector_store.py
Initializes ChromaDB and computes vector embeddings using Google Gemini embeddings.
Enforces hard metadata filtering for Role-Based Access Control (RBAC).
"""

import os
import chromadb
import google.generativeai as genai
from dotenv import load_dotenv
from typing import List, Dict, Any, Optional

# Load configuration from .env
ENV_PATH = os.path.join(os.path.dirname(__file__), ".env")
load_dotenv(ENV_PATH)
api_key = os.getenv("GEMINI_API_KEY")
if not api_key:
    raise ValueError("GEMINI_API_KEY is missing from .env file!")

genai.configure(api_key=api_key)

EMBEDDING_MODEL = os.getenv("EMBEDDING_MODEL", "models/gemini-embedding-001")
DB_DIR = os.getenv("CHROMA_DB_DIR", "./chroma_data")
if not os.path.isabs(DB_DIR):
    DB_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), DB_DIR))

# Initialize persistent local ChromaDB
client = chromadb.PersistentClient(path=DB_DIR)

# Single collection for course knowledge with cosine similarity space
collection = client.get_or_create_collection(
    name="lms_course_knowledge",
    metadata={"hnsw:space": "cosine"}
)


def get_embedding(text: str) -> List[float]:
    """
    Calls Gemini API to generate a vector embedding for a document chunk.
    Uses 'retrieval_document' task type for optimal storage semantics.
    """
    result = genai.embed_content(
        model=EMBEDDING_MODEL,
        content=text,
        task_type="retrieval_document"
    )
    return result["embedding"]


def get_query_embedding(query: str) -> List[float]:
    """
    Generates embedding for a user search query.
    Uses 'retrieval_query' task type to pair with document embeddings.
    """
    result = genai.embed_content(
        model=EMBEDDING_MODEL,
        content=query,
        task_type="retrieval_query"
    )
    return result["embedding"]


def add_documents_to_vector_db(prepared_chunks: List[Dict[str, Any]]):
    """
    Embeds each chunk and upserts into ChromaDB with unique IDs and metadata.
    """
    if not prepared_chunks:
        print("No chunks provided to index.")
        return

    ids = []
    documents = []
    embeddings = []
    metadatas = []

    print(f"Generating embeddings for {len(prepared_chunks)} chunk(s) via Gemini API...")

    for i, item in enumerate(prepared_chunks):
        chunk_text = item["text"]
        meta = item["metadata"]
        
        # Clean unique ID: c{courseId}_{sourceFile}_p{page}_idx{chunkIdx}
        safe_filename = "".join(c for c in meta["source_file"] if c.isalnum() or c in ("_", "-"))
        doc_id = f"c{meta['course_id']}_{safe_filename}_p{meta['page_number']}_idx{meta['chunk_index']}"
        
        vector = get_embedding(chunk_text)
        
        ids.append(doc_id)
        documents.append(chunk_text)
        embeddings.append(vector)
        metadatas.append(meta)

    # Upsert in ChromaDB (creates or updates)
    collection.upsert(
        ids=ids,
        documents=documents,
        embeddings=embeddings,
        metadatas=metadatas
    )

    print(f" Successfully indexed {len(ids)} chunk(s) into ChromaDB!")


def query_vector_db(
    query_text: str,
    course_id: Optional[int] = 1,
    enrolled_course_ids: Optional[List[int]] = None,
    user_role: str = "student",
    top_k: int = 3
) -> List[Dict[str, Any]]:
    """
    HARD RBAC & MULTI-COURSE RETRIEVAL:
    - If course_id > 0: Scoped strictly to that single course.
    - If course_id == 0 / None (Global Mode):
        - For students: Scoped across all courses in enrolled_course_ids.
        - For instructors/admins: Searches across all courses globally.
    - RBAC: Students can only retrieve visibility == 'student'.
    """
    query_vector = get_query_embedding(query_text)

    # 1. Scoped to a specific course
    if course_id and course_id > 0:
        if user_role.lower() == "student":
            where_filter = {
                "$and": [
                    {"course_id": {"$eq": int(course_id)}},
                    {"visibility": {"$eq": "student"}}
                ]
            }
        else:
            where_filter = {"course_id": {"$eq": int(course_id)}}

    # 2. Global / Multi-Course Mode (no specific course open)
    else:
        if user_role.lower() == "student":
            if enrolled_course_ids and len(enrolled_course_ids) > 0:
                if len(enrolled_course_ids) == 1:
                    where_filter = {
                        "$and": [
                            {"course_id": {"$eq": int(enrolled_course_ids[0])}},
                            {"visibility": {"$eq": "student"}}
                        ]
                    }
                else:
                    where_filter = {
                        "$and": [
                            {"course_id": {"$in": [int(cid) for cid in enrolled_course_ids]}},
                            {"visibility": {"$eq": "student"}}
                        ]
                    }
            else:
                # Student has no enrolled courses
                return []
        else:
            # Instructor/Admin in global mode: search all course materials
            where_filter = None

    if where_filter:
        results = collection.query(
            query_embeddings=[query_vector],
            n_results=top_k,
            where=where_filter
        )
    else:
        results = collection.query(
            query_embeddings=[query_vector],
            n_results=top_k
        )

    formatted_results = []
    if results and results["documents"] and len(results["documents"]) > 0:
        docs = results["documents"][0]
        metas = results["metadatas"][0]
        distances = results["distances"][0] if "distances" in results else [0.0] * len(docs)
        
        for doc, meta, dist in zip(docs, metas, distances):
            formatted_results.append({
                "content": doc,
                "metadata": meta,
                "similarity_score": round(1.0 - float(dist), 4)  # Cosine similarity metric
            })

    return formatted_results
