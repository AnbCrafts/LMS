# EduPulse LMS - Artificial Intelligence Services Catalog

This document details all Artificial Intelligence (AI) and Retrieval-Augmented Generation (RAG) services integrated into the EduPulse Learning Management System. It covers models used, vector retrieval architectures, API contracts, and exact source code file locations.

---

## 1. AI Services Master Matrix

| # | Service Name | Primary Model / Engine | Secondary / Fallback | RAG Involved? | Primary Code Files | User Role & UI Location |
|---|---|---|---|:---:|---|---|
| **1** | **Course-Scoped AI Teaching Assistant** | `gemini-3.5-flash-lite`<br>(Hybrid Dense + BM25 RRF) | `gemini-3.1-flash-lite`<br>Tier-3: ChromaDB Extractive | **YES** | `Rag_Service/hybrid_search.py`<br>`Rag_Service/rag_engine.py`<br>`Controllers/ChatbotController.cs`<br>`Views/Courses/Learn.cshtml` | Students enrolled in course<br>One-Click helper & drawer on `/Courses/Learn/{id}` |
| **2** | **Global Multi-Course AI Assistant** | `gemini-3.5-flash-lite`<br>(Hybrid Dense + BM25 RRF) | `gemini-3.1-flash-lite`<br>Tier-3: ChromaDB Extractive | **YES** | `Rag_Service/hybrid_search.py`<br>`Controllers/ChatbotController.cs`<br>`Views/Shared/_ChatWidget.cshtml` | Enrolled Students<br>Available globally on Dashboard / Home (`CourseId=0`) |
| **3** | **Instructor Instructional Design Assistant** | `gemini-3.5-flash-lite` | `gemini-3.1-flash-lite`<br>Tier-3: Extractive Fallback | **YES** | `Rag_Service/rag_engine.py`<br>`Rag_Service/api.py` | Instructors & Admins<br>Elevated persona with solution-key access |
| **4** | **AI Quiz Generator from PDF Lecture Slides** | `gemini-3.5-flash-lite` | Direct Structured JSON output | **NO** | `Rag_Service/api.py`<br>`Controllers/InstructorController.cs`<br>`Views/Instructor/ManageQuiz.cshtml` | Instructors<br>Modal on `/Instructor/ManageQuiz/{id}` |
| **5** | **AI Course Matcher & Career Advisor** | `gemini-3.5-flash-lite`<br>`gemini-embedding-001` | `gemini-3.1-flash-lite`<br>Tier-3: In-Memory Cosine Fallback | **YES** | `Rag_Service/course_recommender.py`<br>`Rag_Service/api.py`<br>`Controllers/CoursesController.cs`<br>`Views/Courses/Index.cshtml` | All Learners & Visitors<br>Banner & Advisor on `/Courses` catalog |
| **6** | **Semantic Vector Caching Engine** | `gemini-embedding-001`<br>(Cosine Similarity $\ge 0.94$) | In-memory Cosine cache (<5ms, 0 LLM calls) | **HYBRID** | `Rag_Service/resilience.py`<br>`Rag_Service/api.py` | Transparent acceleration layer for all chat queries |
| **7** | **Sliding-Window Rate Limiter & Anti-Spam** | In-Memory Sliding Window + MD5 Hashing | HTTP 429 Cooldown Interceptor | **NO** | `Rag_Service/resilience.py`<br>`Rag_Service/api.py`<br>`Controllers/ChatbotController.cs` | Security & Quota Defense for all AI endpoints |
| **8** | **Automated Document Chunking & Vector Ingestion** | `models/gemini-embedding-001`<br>(`retrieval_document` task) | Local Persistent ChromaDB (`hnsw:space: cosine`) | **YES** | `Rag_Service/chunker.py`<br>`Rag_Service/ingest.py`<br>`Rag_Service/vector_store.py`<br>`Rag_Service/ingest_study_materials.py` | Background Ingestion Pipeline for Syllabi & PDFs |
| **9** | **Instructor Operational Analytics & Automated Assignment Evaluator** | `gemini-3.5-flash-lite`<br>(Live SQL Server + File Extraction) | `gemini-3.1-flash-lite`<br>Tier-3: Rule-Based Rubric Fallback | **HYBRID** | `Rag_Service/instructor_tools.py`<br>`Rag_Service/rag_engine.py`<br>`Rag_Service/api.py` | Instructors & Admins<br>Natural language query & assignment evaluation in chat |
| **10** | **Live Study Material Upload & Vector Ingestion API** | `models/gemini-embedding-001`<br>(`retrieval_document` task) | Local Persistent ChromaDB (`lms_course_knowledge`) | **YES** | `Rag_Service/api.py`<br>`Controllers/InstructorController.cs`<br>`Views/Instructor/Modules.cshtml` | Instructors<br>Auto-indexes PDF uploads directly from `/Instructor/Modules` |
| **11** | **Student Learning Friction & Concept Confusion Heatmap** | `gemini-3.5-flash-lite`<br>(Live SQL Inquiries Cluster) | `LMS.StudentQueryLogs`<br>Tier-3: Rule-Based Dean Synthesis | **HYBRID** | `Rag_Service/instructor_tools.py`<br>`Rag_Service/api.py`<br>`Controllers/ChatbotController.cs`<br>`Views/Instructor/Index.cshtml` | Instructors & Admins<br>Real-time analytics card on `/Instructor/Index` & chat |

---

## 2. Detailed Service Specifications

### Service 1: Course-Scoped AI Teaching Assistant (Socratic Tutor)
* **Description**: An AI tutor connected directly to the curriculum of the active course. Follows strict pedagogical anti-cheating guidelines (Socratic method): refuses to provide complete assignment code; instead provides conceptual hints, abstract examples, and exact slide/page citations.
* **Is RAG Involved?**: **YES**.
  * Performs vector retrieval over ChromaDB collection `lms_course_knowledge`.
  * Enforces hard Role-Based Access Control (RBAC): student queries are filtered by `course_id == activeCourseId` AND `visibility == 'student'`.
  * Answers are verified with bracketed citations: `[Source: <filename>, Page: <page_number>]`.
* **Model Cascading**:
  * **Tier 1 (Primary)**: `models/gemini-3.5-flash-lite` (temperature 0.2).
  * **Tier 2 (Secondary Cascade)**: `models/gemini-3.1-flash-lite` (activates if Tier-1 hits rate limits).
  * **Tier 3 (Offline Extractive Fallback)**: Synthesizes retrieved ChromaDB text excerpts directly without calling external APIs (100% uptime guarantee).
* **Where Code is Written**:
  * Python: `Rag_Service/rag_engine.py` (Prompt engineering & cascading logic)
  * Python: `Rag_Service/vector_store.py` (`query_vector_db` with ChromaDB filtering)
  * Python: `Rag_Service/api.py` (`POST /api/chat`)
  * C# Controller: `Controllers/ChatbotController.cs` (`Ask` method with enrollment verification)
  * Razor View: `Views/Shared/_ChatWidget.cshtml` (Floating slide-out drawer)

---

### Service 2: Global Multi-Course AI Assistant
* **Description**: When a learner queries the assistant outside a specific course (e.g. from the Student Dashboard or Home page, `CourseId = 0`), the assistant scopes its knowledge base across **all courses the student has actively enrolled in**, while strictly isolating courses they have not enrolled in.
* **Is RAG Involved?**: **YES**.
  * C# queries SQL Server `LMS.Enrollments` for the logged-in student's active `course_id`s.
  * ChromaDB applies a dynamic `$in` filter:
    ```python
    where = {
        "$and": [
            {"course_id": {"$in": enrolled_course_ids}},
            {"visibility": "student"}
        ]
    }
    ```
* **Models**: `gemini-3.5-flash-lite` + `gemini-embedding-001`.
* **Where Code is Written**:
  * Python: `Rag_Service/vector_store.py` (Multi-course `$in` filter logic)
  * C# Controller: `Controllers/ChatbotController.cs` (Fetches `_enrollmentRepo.GetStudentDashboard(userId)` and passes `enrolled_course_ids` to Python)
  * Razor View: `Views/Shared/_ChatWidget.cshtml` (Displays "Global Assistant - All Enrolled Courses" badge)

---

### Service 3: Instructor Instructional Design Assistant
* **Description**: When an authenticated Instructor or Admin accesses the assistant, the system elevates their persona. The assistant gains access to confidential grading keys, midterm solution manuals, and internal syllabi (`visibility == 'instructor'`), allowing instructors to draft exam questions, rubrics, and lesson plans.
* **Is RAG Involved?**: **YES**.
  * ChromaDB where-filter removes the `visibility == 'student'` restriction, granting access to confidential instructor documents.
* **Models**: `gemini-3.5-flash-lite` (with cascading fallback).
* **Where Code is Written**:
  * Python: `Rag_Service/rag_engine.py` (`INSTRUCTOR_SYSTEM_PROMPT`)
  * Python: `Rag_Service/vector_store.py` (Instructor RBAC bypass)
  * Razor View: `Views/Shared/_ChatWidget.cshtml` (Displays "Instructor Mode" badge)

---

### Service 4: AI Automated Quiz Generator from PDF Lecture Slides
* **Description**: Enables instructors to upload lecture slides or syllabus documents (PDF) and instantly generate pedagogical multiple-choice quizzes with 4 plausible options, correct answer keys, and pedagogical explanations.
* **Is RAG Involved?**: **NO**.
  * Extracts text directly from the uploaded PDF binary stream using `pypdf`.
  * Uses Gemini with few-shot instructions to output a strict JSON array.
  * Results are rendered in an interactive preview table where instructors can review, edit, and insert questions directly into `LMS.QuizQuestions`.
* **Model**: `models/gemini-3.5-flash-lite` (temperature 0.2, structured JSON output).
* **Where Code is Written**:
  * Python: `Rag_Service/api.py` (`POST /api/generate-quiz-from-pdf`)
  * C# Controller: `Controllers/InstructorController.cs` (`ManageQuiz` action)
  * Razor View: `Views/Instructor/ManageQuiz.cshtml` (Upload form + AJAX modal preview)

---

### Service 5: AI Course Matcher & Career Pathway Advisor
* **Description**: On the Course Catalog (`/Courses`), learners can express their freeform interests or career aspirations (e.g., *"I want to build real-time trading dashboards"* or *"I want to learn cloud DevOps"*). The engine analyzes the full syllabus of all courses to identify direct matches or foundational/related courses, providing match percentage scores, personalized rationales, and learning pathway tips.
* **Is RAG Involved?**: **YES**.
  * Pre-computes semantic vector embeddings across all course descriptions, module titles, and curriculum topics.
  * Vector search identifies candidate courses.
  * Gemini synthesizes a tailored learning pathway, categorizing each course as `Direct Match` (88-99%) or `Foundational / Related Match` (65-85%).
* **Models**:
  * Embedding: `models/gemini-embedding-001`
  * Synthesis: `models/gemini-3.5-flash-lite` (with `3.1-flash-lite` and offline cosine fallback)
* **Where Code is Written**:
  * Python: `Rag_Service/course_recommender.py` (`CourseRecommender` class)
  * Python: `Rag_Service/api.py` (`POST /api/recommend-courses`)
  * C# Controller: `Controllers/CoursesController.cs` (`AiRecommend` action)
  * Razor View: `Views/Courses/Index.cshtml` (AI Advisor card, prompt chips, responsive 2-column cards)

---

### Service 6: In-Memory Semantic Vector Caching Engine
* **Description**: Prevents redundant LLM API calls and avoids rate limit exhaustion when multiple students ask identical or conceptually similar questions (e.g., *"What is idempotency?"* vs *"Why is GET idempotent?"*).
* **Is RAG Involved?**: **HYBRID**.
  * Checks incoming query vector embedding against an in-memory cosine similarity cache.
  * If cosine similarity is $\ge 0.94$, the cached verified response is returned in **<5ms with 0 LLM calls and 0 token cost**.
* **Model**: `models/gemini-embedding-001` (embeddings only) + in-memory mathematical cosine calculation.
* **Where Code is Written**:
  * Python: `Rag_Service/resilience.py` (`SemanticCache` class)
  * Python: `Rag_Service/api.py` (Interception logic inside `handle_chat`)
  * Razor View: `Views/Shared/_ChatWidget.cshtml` (Displays `Instant Semantic Cache (0ms • 0 API calls)` badge)

---

### Service 7: Sliding-Window Rate Limiter & Duplicate Dropper
* **Description**: Protects the platform from rapid-fire spam scripts and accidental double-clicks.
  * Bounds each authenticated user to **5 requests per 30-second window**.
  * Caches MD5 hashes of `(user_key + query)` for 15 seconds to return immediate responses on accidental double-clicks in **3.5ms**.
  * Rejects burst spam with **HTTP 429** and dynamic cooldown guidance.
* **Is RAG Involved?**: **NO** (Gateway defense layer).
* **Where Code is Written**:
  * Python: `Rag_Service/resilience.py` (`RateLimiter` class)
  * Python: `Rag_Service/api.py` (Enforced before RAG engine or cache lookup)
  * C# Controller: `Controllers/ChatbotController.cs` (Parses HTTP 429 JSON detail for user notification)

---

### Service 8: Automated Document Chunking & Vector Ingestion
* **Description**: Offline and automated ingestion pipeline that reads course syllabi, lecture notes (Markdown and PDF), splits text into overlapping semantic chunks (500 characters, 100 character overlap), generates vector embeddings via Gemini, and indexes them into local persistent ChromaDB.
* **Is RAG Involved?**: **YES** (The data preparation layer for all RAG retrieval).
* **Model**: `models/gemini-embedding-001` (`retrieval_document` task type).
* **Where Code is Written**:
  * Python: `Rag_Service/chunker.py` (Text cleaning and sliding-window chunking)
  * Python: `Rag_Service/vector_store.py` (`get_embedding`, `add_documents_to_vector_db`)
  * Python: `Rag_Service/ingest.py` (Directory crawler for sample notes)
  * Python: `Rag_Service/ingest_study_materials.py` (Database-driven syllabus ingester)

---

## 3. Communication & Architecture Flow

```
+-----------------------------------------------------------------------------------+
|                            ASP.NET MVC 5 (Port 57794)                             |
|                                                                                   |
|   [ChatbotController.cs]       [CoursesController.cs]    [InstructorController.cs]|
|     - Course/Global Chat        - AI Course Advisor       - PDF Quiz Generator    |
|     - RBAC Enrollment Check     - Freeform Matching       - Module Study Materials|
+------------------------------------------+----------------------------------------+
                                           | HTTP REST (JSON)
                                           v
+-----------------------------------------------------------------------------------+
|                        Python FastAPI Service (Port 8000)                         |
|                                                                                   |
|   [resilience.py]           [rag_engine.py]            [course_recommender.py]    |
|   - Rate Limiter (5/30s)    - 3-Tier Model Cascade     - Semantic Embedding Match |
|   - Duplicate Dropper       - Socratic Anti-Cheat      - Transferable Skill Ranks |
|   - Semantic Cache (>=0.94) - Citation Parser          - Pathway Tip Generator    |
+------------------------------------------+----------------------------------------+
                     |                                       |
                     v                                       v
+---------------------------------------+   +---------------------------------------+
|        Persistent ChromaDB            |   |       Google Gemini API (Cloud)       |
|                                       |   |                                       |
| - Collection: lms_course_knowledge    |   | - models/gemini-3.5-flash-lite (Tier 1|
| - Cosine Distance Space               |   | - models/gemini-3.1-flash-lite (Tier 2|
| - Metadata: course_id, visibility     |   | - models/gemini-embedding-001         |
+---------------------------------------+   +---------------------------------------+
```

---

## 4. Environment Variables Required

All AI microservices rely on the `.env` configuration located at `Rag_Service/.env`:

```env
GEMINI_API_KEY=your_google_gemini_api_key_here
CHAT_MODEL=models/gemini-3.5-flash-lite
EMBEDDING_MODEL=models/gemini-embedding-001
CHROMA_DB_DIR=./chroma_data
```
