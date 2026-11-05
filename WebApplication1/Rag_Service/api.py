"""
api.py
FastAPI microservice for LMS RAG Chatbot and AI Quiz Generator.
Runs on http://127.0.0.1:8000
"""

import io
import json
import pypdf
import google.generativeai as genai
from fastapi import FastAPI, HTTPException, UploadFile, File, Form
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Optional, List, Dict, Any
from rag_engine import RAGEngine
from resilience import rate_limiter, semantic_cache
from vector_store import get_query_embedding
from course_recommender import recommender

app = FastAPI(title="EduPulse LMS RAG Assistant API", version="1.0")

# Enable CORS for local MVC application calls
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

engine = RAGEngine()


class ChatRequest(BaseModel):
    query: str
    course_id: Optional[int] = 0
    enrolled_course_ids: Optional[List[int]] = None
    user_id: Optional[str] = "default_user"
    user_role: Optional[str] = "student"
    top_k: Optional[int] = 3


class CitationItem(BaseModel):
    source_file: Optional[str] = None
    page_number: Optional[int] = 1


class SourceUsedItem(BaseModel):
    source_file: Optional[str] = None
    page_number: Optional[int] = 1
    similarity: Optional[float] = 0.0
    visibility: Optional[str] = "student"


class ChatResponse(BaseModel):
    query: str
    answer: str
    role: str
    course_id: int
    citations: List[Dict[str, Any]]
    sources_count: int
    sources_used: List[Dict[str, Any]]
    cached: Optional[bool] = False
    model_used: Optional[str] = "models/gemini-3.5-flash-lite"
    cache_similarity: Optional[float] = None


@app.get("/health")
def health_check():
    return {
        "status": "online",
        "service": "EduPulse RAG Assistant",
        "model": engine.model_name
    }


@app.post("/api/chat", response_model=ChatResponse)
def handle_chat(req: ChatRequest):
    if not req.query or not req.query.strip():
        raise HTTPException(status_code=400, detail="Query cannot be empty.")
    
    clean_q = req.query.strip()
    if len(clean_q) <= 1 and not clean_q.isalnum():
        raise HTTPException(status_code=400, detail="Please enter a meaningful question or topic.")
    
    # 1. Anti-Spam & Burst Rate Limiting (Max 5 queries per 30s)
    user_key = f"{req.user_id or 'anon'}_{req.user_role or 'student'}"
    is_allowed, retry_after, limit_msg = rate_limiter.check_rate_limit(user_key)
    if not is_allowed:
        raise HTTPException(status_code=429, detail=limit_msg)

    # 2. Duplicate Request Prevention (15s cooldown for identical query spam)
    cached_dup = rate_limiter.check_duplicate(user_key, req.query)
    if cached_dup:
        return cached_dup

    course_key = req.course_id if req.course_id is not None else 0
    q_vec = None
    is_instructor = (req.user_role or "student").lower() in ("instructor", "admin")

    # 3. High-Speed Semantic Cache Lookup (<5ms, zero token cost) - Only for static curriculum queries
    if not is_instructor:
        try:
            q_vec = get_query_embedding(req.query)
            cached_semantic = semantic_cache.get(req.query, q_vec, course_key)
            if cached_semantic:
                rate_limiter.store_duplicate(user_key, req.query, cached_semantic)
                return cached_semantic
        except Exception as emb_err:
            print(f"[Cache Lookup Warning] Embedding generation failed for cache lookup: {emb_err}")

    # 4. Fall through to RAG Engine with Cascading Model Tiers & Agentic Tools
    try:
        result = engine.answer_query(
            query=req.query,
            course_id=course_key,
            enrolled_course_ids=req.enrolled_course_ids,
            user_role=req.user_role or "student",
            top_k=req.top_k or 3,
            user_id=req.user_id
        )
        result["cached"] = False
        result["cache_similarity"] = None

        # 5. Save verified answer to Semantic Cache (for static student queries)
        if not is_instructor and q_vec and result.get("model_used") not in ("offline-extractive-fallback", "security-guardrail-interceptor", "grounding-threshold-guardrail"):
            semantic_cache.put(req.query, q_vec, course_key, result)
        rate_limiter.store_duplicate(user_key, req.query, result)

        return result
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/api/generate-quiz-from-pdf")
async def generate_quiz_from_pdf(
    file: UploadFile = File(...),
    num_questions: int = Form(3),
    lesson_number: int = Form(1),
    topic: Optional[str] = Form("")
):
    """
    Accepts an uploaded PDF (lecture slides, notes, syllabus)
    Extracts text and uses Gemini to generate pedagogical multiple-choice quiz questions.
    """
    if not file.filename.lower().endswith(".pdf"):
        raise HTTPException(status_code=400, detail="Only PDF files are supported.")
    
    content = await file.read()
    if len(content) == 0:
        raise HTTPException(status_code=400, detail="Uploaded file is empty.")
    
    try:
        reader = pypdf.PdfReader(io.BytesIO(content))
        text_pages = [p.extract_text() for p in reader.pages if p.extract_text()]
        full_text = "\n\n".join(text_pages).strip()
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Failed to read PDF document: {str(e)}")
    
    if len(full_text) < 50:
        raise HTTPException(
            status_code=400,
            detail="The uploaded PDF contains insufficient extractable text. Please ensure it is a text-based PDF rather than a scanned image."
        )
    
    # Cap text length for low-latency generation
    truncated_text = full_text[:25000]
    num_q = max(1, min(10, num_questions))
    
    prompt = f"""You are an expert curriculum designer and university examiner.
Generate exactly {num_q} high-quality multiple choice quiz questions based SOLELY on the lecture material below.
{f'Topic focus: {topic}' if topic else ''}

EACH QUESTION MUST HAVE:
- Question text (clear, testing conceptual understanding and application)
- 4 distinct plausible options labeled option_a, option_b, option_c, option_d
- Correct option letter ('A', 'B', 'C', or 'D')
- A brief pedagogical explanation citing concepts from the text

OUTPUT FORMAT:
Return ONLY a valid JSON array of objects with these exact keys:
[
  {{
    "question_text": "...",
    "option_a": "...",
    "option_b": "...",
    "option_c": "...",
    "option_d": "...",
    "correct_option": "A",
    "explanation": "..."
  }}
]

LECTURE MATERIAL:
{truncated_text}
"""
    try:
        model = genai.GenerativeModel(engine.model_name)
        response = model.generate_content(prompt)
        raw_text = response.text.strip()
        
        # Clean markdown code fences if wrapped
        if raw_text.startswith("```json"):
            raw_text = raw_text[7:]
        elif raw_text.startswith("```"):
            raw_text = raw_text[3:]
        if raw_text.endswith("```"):
            raw_text = raw_text[:-3]
        
        questions = json.loads(raw_text.strip())
        return {
            "success": True,
            "filename": file.filename,
            "source_pages": len(reader.pages),
            "total_questions": len(questions),
            "lesson_number": lesson_number,
            "questions": questions
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"AI Quiz Generation failed: {str(e)}")


class RecommendCoursesRequest(BaseModel):
    interests: str
    learner_level: Optional[str] = "All"
    top_k: Optional[int] = 3


@app.post("/api/recommend-courses")
def handle_recommend_courses(req: RecommendCoursesRequest):
    if not req.interests or not req.interests.strip():
        raise HTTPException(status_code=400, detail="Please specify your interests or learning goals.")
    
    try:
        result = recommender.recommend(
            interests=req.interests.strip(),
            user_level=req.learner_level or "All",
            top_k=req.top_k or 3
        )
        return result
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Course recommendation failed: {str(e)}")


@app.get("/api/instructor/friction-heatmap/{instructor_id}")
def get_friction_heatmap_endpoint(instructor_id: int, course_id: Optional[int] = None):
    """
    Returns aggregated student friction metrics, difficulty levels, and AI recommendations.
    """
    try:
        from instructor_tools import get_student_friction_data, get_student_friction_report
        data = get_student_friction_data(instructor_id, course_id)
        report = get_student_friction_report(instructor_id, course_id)
        data["report_markdown"] = report
        return data
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to fetch friction heatmap: {str(e)}")


@app.post("/api/ingest-study-material")
async def ingest_study_material_endpoint(
    file: UploadFile = File(...),
    course_id: int = Form(1),
    visibility: str = Form("student"),
    doc_type: str = Form("lecture_notes")
):
    """
    Accepts an uploaded PDF study material, chunks it, generates vector embeddings,
    and indexes it into ChromaDB with course_id and RBAC metadata in real time.
    """
    if not file.filename.lower().endswith(".pdf"):
        raise HTTPException(status_code=400, detail="Only PDF documents can be ingested into vector search.")
    
    content = await file.read()
    if len(content) == 0:
        raise HTTPException(status_code=400, detail="Uploaded file is empty.")
    
    import tempfile
    import os
    
    with tempfile.NamedTemporaryFile(delete=False, suffix=".pdf") as tmp:
        tmp.write(content)
        tmp_path = tmp.name

    try:
        from chunker import prepare_documents_for_indexing
        from vector_store import add_documents_to_vector_db
        
        chunks = prepare_documents_for_indexing(
            file_path=tmp_path,
            course_id=int(course_id),
            visibility=visibility,
            doc_type=doc_type
        )
        
        # Override source_file metadata to use real filename
        for c in chunks:
            if "metadata" in c:
                c["metadata"]["source_file"] = file.filename
                
        if chunks:
            add_documents_to_vector_db(chunks)
            try:
                from hybrid_search import bm25_engine
                bm25_engine.build_index(force_refresh=True)
            except Exception as bm_err:
                print(f"[BM25 Refresh Warning] {bm_err}")
            
        return {
            "success": True,
            "filename": file.filename,
            "course_id": course_id,
            "chunks_indexed": len(chunks)
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Ingestion failed: {str(e)}")
    finally:
        if os.path.exists(tmp_path):
            try:
                os.remove(tmp_path)
            except:
                pass


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("api:app", host="127.0.0.1", port=8000, reload=False)
