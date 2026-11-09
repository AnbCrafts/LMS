# EduPulse LMS: AI Capabilities & RAG Architecture Specification

> **Subsystem Target:** Intelligent Tutoring, Instructional Design Automation & Semantic Retrieval  
> **Engine:** FastAPI Microservice (Port 8000) powered by Google Gemini Models (`gemini-3.5-flash-lite`, `gemini-3.8-flash`, `gemini-embedding-001`) and ChromaDB.

---

## 1. High-Level AI Architecture & RAG Pipeline

EduPulse integrates an **Enterprise Agentic Retrieval-Augmented Generation (RAG)** pipeline designed for academic integrity, multi-role security, zero hallucination on lecture materials, and ultra-low latency.

```mermaid
flowchart TD
    UserQuery["Student / Instructor Query"] --> Guardrails["Prompt Injection Guardrail (security_guardrails.py)"]
    Guardrails -->|Blocked / Jailbreak| BlockedResp["Return Pedagogical Warning"]
    Guardrails -->|Safe Query| RateLimiter["Sliding-Window Rate Limiter (resilience.py)"]
    RateLimiter -->|Burst / Spammer| CooldownResp["Return 429 Cooldown Notice"]
    RateLimiter -->|Allowed| SemanticCache["Semantic Response Cache (resilience.py)"]
    
    SemanticCache -->|Hit (Similarity >= 0.96)| CachedResp["Instant Response (5ms Latency, 0 API Calls)"]
    SemanticCache -->|Miss| Router["Query Intent Classifier & Router (rag_engine.py)"]
    
    subgraph Hybrid_Retrieval ["Hybrid Search Engine (hybrid_search.py)"]
        Router --> DenseSearch["Dense Vector Search (ChromaDB + Gemini Embeddings)"]
        Router --> SparseSearch["Sparse Lexical Search (BM25Okapi Engine)"]
        DenseSearch & SparseSearch --> RRF["Reciprocal Rank Fusion (RRF Re-Ranker)"]
        RRF --> RBACFilter{"RBAC Filter Enforced\n(visibility == 'student' vs 'instructor')"}
    end
    
    RBACFilter --> ContextAssembly["Context Builder & Prompt Assembler"]
    
    subgraph Tiered_Cascading ["Resilient Model Cascading (resilience.py)"]
        ContextAssembly --> PrimaryModel["Primary: gemini-3.5-flash-lite (Cost & Latency Optimized)"]
        PrimaryModel -->|Timeout / Quota Error| SecondaryModel["Secondary: gemini-3.8-flash (Frontier Reasoning)"]
        SecondaryModel -->|Network Failure| TertiaryFallback["Tertiary: Local Extractive Notes Fallback"]
    end
    
    PrimaryModel & SecondaryModel & TertiaryFallback --> OutputFormatter["Citation Formatter & LaTeX/KaTeX Parser"]
    OutputFormatter --> CacheUpdate["Update Semantic Cache"]
    CacheUpdate --> FinalResponse["Deliver Grounded Markdown Response + Citation Pills"]
```

---

## 2. The 5 Core AI Facilities Served

EduPulse provides five dedicated AI services targeting different stages of the educational lifecycle:

### 2.1. Socratic Student Teaching Assistant
* **Purpose:** Acts as a 24/7 dedicated course tutor embedded in the classroom (`Learn.cshtml`).
* **Pedagogical Rule:** Forbids providing direct copy-paste solutions to homework or quiz assignments. Instead, follows the **Socratic method**: highlights conceptual mistakes, provides illustrative analogies, offers progressive hints, and cites exact lecture slide pages.
* **Code Files Used:**
  * `rag_engine.py` (Method: `answer_query()`)
  * `hybrid_search.py` (Method: `query_hybrid()`)
  * `security_guardrails.py` (Method: `enforce_student_pedagogy()`)
  * `Views/Shared/_ChatWidget.cshtml` (Client interactive UI)

### 2.2. Semantic Course Advisor & Recommender
* **Purpose:** Analyzes a learner's background, desired career paths, or skill gaps, and matches them to the catalog using vector similarity across module syllabi.
* **Algorithm:** Computes cosine similarity between user aspiration embeddings and course vector centroids, returning structured match scores (e.g., *"98% Match for Frontend Engineer path"*).
* **Code Files Used:**
  * `course_recommender.py` (Class: `CourseRecommender`)
  * `vector_store.py` (Method: `get_query_embedding()`)
  * `Controllers/CoursesController.cs` (Action: `AiRecommend`)

### 2.3. Instructor AI Quiz Generator from PDF Lecture Slides
* **Purpose:** Enables instructors to generate high-fidelity multiple-choice quizzes with a single click during module authoring.
* **Mechanism:** Reads the uploaded lesson PDF (e.g., React 18 Concurrent Slides), extracts core technical arguments, and prompts Gemini to construct 3–5 multiple-choice questions with plausible distractors, correct answers, and pedagogical rationales referencing the slide text.
* **Code Files Used:**
  * `instructor_tools.py` (Method: `generate_quiz_from_pdf()`)
  * `api.py` (Endpoint: `/api/instructor/quiz/generate-from-pdf`)
  * `Views/Instructor/ManageQuiz.cshtml` and `Modules.cshtml`

### 2.4. Instructor AI Assignment Evaluator & Rubric Assistant
* **Purpose:** Accelerates assignment grading by evaluating student text or PDF submissions against the instructor's assignment guidelines and lecture slide benchmarks.
* **Output:** Generates a suggested score (0–100), key strengths, areas for technical improvement, and an encouraging constructive feedback comment.
* **Code Files Used:**
  * `instructor_tools.py` (Method: `evaluate_submission()`)
  * `api.py` (Endpoint: `/api/instructor/submissions/evaluate`)
  * `Views/Instructor/Submissions.cshtml`

### 2.5. Academic Study Guide & Document Synthesizer
* **Purpose:** Synthesizes long-form curriculum documents into concise exam review cheat sheets and structured conceptual roadmaps for quick student review.
* **Code Files Used:**
  * `generate_study_materials.py`
  * `chunker.py` (Method: `recursive_chunk()`)
  * `ingest_study_materials.py`

---

## 3. How RAG is Implemented: Step-by-Step Pipeline

### Step 1: Document Parsing & Semantic Chunking (`chunker.py`)
Course lecture slides and study guides (PDF, Markdown, TXT) are parsed and split into overlapping windows:
* **Target Chunk Size:** 600–800 tokens (~2,500 characters).
* **Overlap:** 100 tokens (prevents context truncation across page borders).
* **Metadata Enriched per Chunk:**
  ```python
  {
      "source_file": "Course1_React18_Architecture_Cheatsheet.pdf",
      "course_id": 1,
      "module_id": 1,
      "page": 2,
      "visibility": "student",          # 'student' or 'instructor_only'
      "content_type": "lecture_slides"
  }
  ```

### Step 2: Vector Embedding Generation (`vector_store.py`)
* **Embedding Model:** Google `models/gemini-embedding-001`.
* **Output Vector Dimension:** 768 / 1536 float values.
* **Vector Store:** ChromaDB persistent client (`./chroma_data`) with Cosine distance metric (`hnsw:space = cosine`).
* **Ingestion Script:** Executed via `python ingest_study_materials.py` to index 27 semantic lecture chunks.

### Step 3: Hybrid Search (Dense Vector + Sparse BM25) (`hybrid_search.py`)
Rather than relying solely on dense semantic search (which can miss exact function names, syntax keywords, or acronyms like `CSRF`, `ACID`, `OWIN`), EduPulse executes a **Dual Hybrid Retrieval**:
1. **Dense Retrieval:** ChromaDB fetches top-K chunks by semantic cosine proximity.
2. **Sparse Retrieval:** `rank_bm25` (BM25Okapi) scores exact keyword frequency and document rarity across the corpus.
3. **Reciprocal Rank Fusion (RRF):** Combines the rank scores:
   $$RRF(d) = \sum_{m \in M} \frac{1}{60 + \text{rank}_m(d)}$$
   The fused results guarantee both semantic understanding and keyword precision.

### Step 4: Strict RBAC Metadata Boundary Enforcement
Before retrieved excerpts reach the LLM, the engine applies an immovable metadata boundary:
```python
# Student Query Filter
filter_condition = {
    "$and": [
        {"course_id": course_id},
        {"visibility": "student"}   # INSTRUCTOR_ONLY chunks are strictly stripped
    ]
}
```
If a student asks: *"What is the answer key for the midterm in lecture 4?"*, the retriever cannot even fetch the instructor solution chunk, completely neutralizing answer leakage at the database layer.

### Step 5: Socratic Prompt Assembly & System Guardrails (`rag_engine.py`)
The system builds a dynamic system prompt depending on user role:

```markdown
You are the dedicated AI Teaching Assistant for [COURSE_NAME].
Your objective is to help learners understand course concepts based strictly on the provided materials.

Guidelines:
1. Base your answer strictly on the provided Context excerpts.
2. If the context does not contain the answer, reply: "I cannot find this information in the course materials. Please consult your instructor or course forum."
3. If the student asks for direct solutions to an assignment or quiz, DO NOT provide the full solution or code. Instead, explain the underlying concept, provide a conceptual hint, and cite the relevant slide.
4. Always cite your source with exact tags: "[Source: filename.pdf, Page: X]".
```

### Step 6: Resilient Model Cascading & Delivery (`resilience.py`)
1. **Primary Model:** Calls `models/gemini-3.5-flash-lite` (average latency: 500–800ms).
2. **Fallback Cascading:** If the primary model exceeds a 5-second timeout or hits rate limits, the request automatically cascades to `models/gemini-3.8-flash`.
3. **Zero API Semantic Cache:** If an identical or highly similar question (cosine similarity $\ge 0.96$) is asked within 24 hours, the cached answer is served instantly in ~5ms.

---

## 4. Mapping of AI Facilities to Code Files

| Facility / Feature | Primary Backend Code File | Secondary / Supporting File | Frontend Integration |
| :--- | :--- | :--- | :--- |
| **Socratic Q&A Bot** | `Rag_Service/rag_engine.py` | `hybrid_search.py`, `vector_store.py` | `Views/Shared/_ChatWidget.cshtml` |
| **Prompt Guardrails** | `Rag_Service/security_guardrails.py`| `rag_engine.py` | Floating Chat Notification |
| **Rate Limiter & Spam Defense** | `Rag_Service/resilience.py` | `api.py` | HTTP 429 Status Alert |
| **Semantic Response Cache** | `Rag_Service/resilience.py` | `vector_store.py` | `_ChatWidget.cshtml` (5ms indicator) |
| **Course Recommender** | `Rag_Service/course_recommender.py` | `Controllers/CoursesController.cs` | `Views/Courses/Index.cshtml` |
| **AI Quiz Generation** | `Rag_Service/instructor_tools.py` | `api.py`, `pypdf` | `Views/Instructor/ManageQuiz.cshtml` |
| **Assignment Evaluation**| `Rag_Service/instructor_tools.py` | `Controllers/InstructorController.cs`| `Views/Instructor/Submissions.cshtml` |
| **Model Cascading** | `Rag_Service/resilience.py` | `google-generativeai` client | Backend Failover Logging |
