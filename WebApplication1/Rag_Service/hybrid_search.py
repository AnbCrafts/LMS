"""
hybrid_search.py
Enterprise Hybrid Search Engine: Dense Semantic Embeddings + Sparse BM25 Keyword Search
Fuses dense and sparse candidate rankings using Reciprocal Rank Fusion (RRF).
Enforces hard Role-Based Access Control (RBAC) across both retrieval tiers.
"""

import re
import math
from typing import List, Dict, Any, Optional
from rank_bm25 import BM25Okapi

from vector_store import collection, query_vector_db


def tokenize_code_and_text(text: str) -> List[str]:
    """
    Tokenizes text while preserving programming keywords, camelCase words,
    acronyms, and symbols relevant in technical curriculum.
    E.g. 'ActionFilterAttribute' -> ['actionfilterattribute', 'action', 'filter', 'attribute']
    """
    if not text:
        return []
    
    # 1. Extract base alphanumeric tokens + underscores + hyphens
    raw_tokens = re.findall(r"[A-Za-z0-9_\-\.]+", text.lower())
    
    # 2. Extract split subwords for camelCase identifiers
    expanded = []
    for tok in raw_tokens:
        expanded.append(tok)
        # Split CamelCase or PascalCase subcomponents
        subwords = re.findall(r"[A-Z]?[a-z]+|[A-Z]+(?=[A-Z][a-z]|\d|\W|$)|\d+", text)
        for sw in subwords:
            sw_lower = sw.lower()
            if len(sw_lower) > 2 and sw_lower != tok:
                expanded.append(sw_lower)
                
    return expanded


class BM25SearchEngine:
    def __init__(self):
        self.doc_ids: List[str] = []
        self.documents: List[str] = []
        self.metadatas: List[Dict[str, Any]] = []
        self.bm25: Optional[BM25Okapi] = None
        self._is_indexed = False

    def build_index(self, force_refresh: bool = False):
        """
        Loads all chunks from ChromaDB and builds the sparse BM25 index.
        """
        if self._is_indexed and not force_refresh:
            return

        all_data = collection.get(include=["documents", "metadatas"])
        self.doc_ids = all_data.get("ids", [])
        self.documents = all_data.get("documents", [])
        self.metadatas = all_data.get("metadatas", [])

        if not self.documents:
            print("[BM25] No documents found in ChromaDB collection.")
            self.bm25 = None
            self._is_indexed = True
            return

        tokenized_corpus = [tokenize_code_and_text(doc) for doc in self.documents]
        self.bm25 = BM25Okapi(tokenized_corpus)
        self._is_indexed = True
        print(f"[BM25] Successfully built sparse index over {len(self.documents)} chunks.")

    def search(
        self,
        query: str,
        course_id: Optional[int] = 1,
        enrolled_course_ids: Optional[List[int]] = None,
        user_role: str = "student",
        top_k: int = 5
    ) -> List[Dict[str, Any]]:
        """
        Sparse BM25 search with metadata RBAC filtering.
        """
        if not self._is_indexed or self.bm25 is None:
            self.build_index()

        if self.bm25 is None or not self.documents:
            return []

        tokenized_query = tokenize_code_and_text(query)
        if not tokenized_query:
            return []

        # Raw BM25 scores across all documents
        raw_scores = self.bm25.get_scores(tokenized_query)

        # Apply RBAC & Course Filters
        role_lower = (user_role or "student").lower()
        filtered_results = []

        for idx, (score, doc_id, doc_text, meta) in enumerate(
            zip(raw_scores, self.doc_ids, self.documents, self.metadatas)
        ):
            if score <= 0.001:
                continue

            # RBAC check: students cannot see instructor_only docs
            doc_visibility = meta.get("visibility", "student")
            if role_lower == "student" and doc_visibility == "instructor_only":
                continue

            doc_course_id = int(meta.get("course_id", 0))

            # Course scoping
            if course_id and course_id > 0:
                if doc_course_id != int(course_id):
                    continue
            else:
                # Global Mode
                if role_lower == "student":
                    if not enrolled_course_ids or doc_course_id not in [int(c) for c in enrolled_course_ids]:
                        continue

            filtered_results.append({
                "doc_id": doc_id,
                "content": doc_text,
                "metadata": meta,
                "bm25_score": round(float(score), 4)
            })

        # Sort descending by BM25 score
        filtered_results.sort(key=lambda x: x["bm25_score"], reverse=True)
        return filtered_results[:top_k]


# Singleton instance
bm25_engine = BM25SearchEngine()


def query_hybrid(
    query_text: str,
    course_id: Optional[int] = 1,
    enrolled_course_ids: Optional[List[int]] = None,
    user_role: str = "student",
    top_k: int = 3,
    dense_weight: float = 0.55,
    sparse_weight: float = 0.45,
    rrf_k: int = 60
) -> List[Dict[str, Any]]:
    """
    Enterprise Hybrid Retrieval:
    1. Dense Vector Search (ChromaDB + Gemini text-embedding-001)
    2. Sparse Keyword Search (BM25Okapi)
    3. Reciprocal Rank Fusion (RRF) Ranking
    """
    candidate_k = max(top_k * 3, 6)

    # 1. Retrieve Dense Candidates
    dense_results = query_vector_db(
        query_text=query_text,
        course_id=course_id,
        enrolled_course_ids=enrolled_course_ids,
        user_role=user_role,
        top_k=candidate_k
    )

    # 2. Retrieve Sparse BM25 Candidates
    sparse_results = bm25_engine.search(
        query=query_text,
        course_id=course_id,
        enrolled_course_ids=enrolled_course_ids,
        user_role=user_role,
        top_k=candidate_k
    )

    # 3. Fuse with Reciprocal Rank Fusion (RRF)
    # RRF Score = dense_weight / (rrf_k + r_dense) + sparse_weight / (rrf_k + r_sparse)
    fused_scores: Dict[str, Dict[str, Any]] = {}

    # Helper key for deduplication: source_file + page_number + chunk_index
    def get_chunk_key(meta: Dict[str, Any], content: str) -> str:
        s_file = meta.get("source_file", "")
        p_num = meta.get("page_number", 0)
        c_idx = meta.get("chunk_index", 0)
        c_id = meta.get("course_id", 0)
        return f"{c_id}_{s_file}_p{p_num}_c{c_idx}" if s_file else content[:60]

    # Process Dense ranks
    for rank, item in enumerate(dense_results, start=1):
        key = get_chunk_key(item["metadata"], item["content"])
        dense_rrf = dense_weight / (rrf_k + rank)
        fused_scores[key] = {
            "content": item["content"],
            "metadata": item["metadata"],
            "similarity_score": item.get("similarity_score", 0.0),
            "bm25_score": 0.0,
            "rrf_score": dense_rrf,
            "dense_rank": rank,
            "sparse_rank": None,
            "retrieval_mode": "dense"
        }

    # Process Sparse BM25 ranks
    for rank, item in enumerate(sparse_results, start=1):
        key = get_chunk_key(item["metadata"], item["content"])
        sparse_rrf = sparse_weight / (rrf_k + rank)
        if key in fused_scores:
            fused_scores[key]["rrf_score"] += sparse_rrf
            fused_scores[key]["bm25_score"] = item["bm25_score"]
            fused_scores[key]["sparse_rank"] = rank
            fused_scores[key]["retrieval_mode"] = "hybrid (dense+bm25)"
        else:
            fused_scores[key] = {
                "content": item["content"],
                "metadata": item["metadata"],
                "similarity_score": 0.0,
                "bm25_score": item["bm25_score"],
                "rrf_score": sparse_rrf,
                "dense_rank": None,
                "sparse_rank": rank,
                "retrieval_mode": "bm25_sparse"
            }

    # Sort candidates by combined RRF score descending
    fused_list = list(fused_scores.values())
    fused_list.sort(key=lambda x: x["rrf_score"], reverse=True)

    final_results = fused_list[:top_k]

    # Normalize similarity score for downstream display
    for item in final_results:
        if item["similarity_score"] == 0.0 and item["bm25_score"] > 0:
            # Map BM25 score to reasonable range
            item["similarity_score"] = round(min(0.95, 0.5 + (item["bm25_score"] / 20.0)), 4)

    return final_results
