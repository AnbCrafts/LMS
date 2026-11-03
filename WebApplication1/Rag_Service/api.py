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

    # 3. High-Speed Semantic Cache Lookup (<5ms, zero token cost)
    try:
        q_vec = get_query_embedding(req.query)
        cached_semantic = semantic_cache.get(req.query, q_vec, course_key)
        if cached_semantic:
            rate_limiter.store_duplicate(user_key, req.query, cached_semantic)
            return cached_semantic
    except Exception as emb_err:
        print(f"[Cache Lookup Warning] Embedding generation failed for cache lookup: {emb_err}")

    # 4. Fall through to RAG Engine with Cascading Model Tiers
    try:
        result = engine.answer_query(
            query=req.query,
            course_id=course_key,
            enrolled_course_ids=req.enrolled_course_ids,
            user_role=req.user_role or "student",
            top_k=req.top_k or 3
        )
        result["cached"] = False
        result["cache_similarity"] = None

        # 5. Save verified answer to Semantic Cache and Duplicate Cache
        if q_vec and result.get("model_used") != "offline-extractive-fallback":
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


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("api:app", host="127.0.0.1", port=8000, reload=False)
