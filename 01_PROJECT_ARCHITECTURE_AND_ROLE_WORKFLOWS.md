# EduPulse LMS: Architecture, Workflows & File Catalog

> **Target Platform:** EduPulse Enterprise Learning Management System  
> **Architecture Style:** Monolithic Web Core (ASP.NET MVC 5) + Microservice AI Subsystem (FastAPI) + Asynchronous Job Engine (Hangfire) + Distributed Cache (Redis)

---

## 1. High-Level Architecture Overview

EduPulse is engineered as a high-concurrency learning platform that separates synchronous user-facing web operations from heavy computational workloads (RAG embeddings, LLM generation, catalog pre-warming, and enrollment post-processing).

```mermaid
flowchart TB
    subgraph Client_Layer ["Client Layer (Browser)"]
        UI_Guest["Guest / Visitor"]
        UI_Student["Student Workspace"]
        UI_Inst["Instructor Studio"]
        UI_Admin["Admin Console"]
    end

    subgraph Security_Gate ["Security & DRM Guard"]
        DRM_Guard["ep-media-guard.js & CSS (Anti-Save / Anti-Rip / Watermark)"]
        RBAC_Filter["ASP.NET MVC Role Authorization Filters"]
    end

    subgraph App_Server ["Core Web Tier (IIS Express / .NET 4.7.2)"]
        MVC_Controllers["Controllers (Courses, Account, Instructor, Admin, Payment)"]
        Repo_Layer["Repositories (CourseRepo, InstructorRepo, DbHelper)"]
        Hangfire_Server["Hangfire Background Job Server (In-Process)"]
        Redis_Service["RedisCacheService (StackExchange.Redis)"]
    end

    subgraph Data_Tier ["Persistence & In-Memory Store"]
        SQL_DB[("SQL Server: Practice DB (LMS Schema + HangFire Schema)")]
        Redis_Cache[("Redis Server (Port 6379): In-Memory Cache")]
    end

    subgraph AI_Microservice ["AI & Knowledge Subsystem (Port 8000)"]
        FastAPI_App["FastAPI Microservice (api.py)"]
        RAG_Engine["RAG Retrieval Engine (Dense + BM25 Hybrid)"]
        Vector_DB[("ChromaDB Vector Store: ./chroma_data")]
        LLM_Provider["Google Gemini API (Flash-Lite / Flash / Embeddings)"]
    end

    Client_Layer --> Security_Gate
    Security_Gate --> MVC_Controllers
    MVC_Controllers --> Repo_Layer
    Repo_Layer --> SQL_DB
    MVC_Controllers --> Redis_Service
    Redis_Service <--> Redis_Cache
    MVC_Controllers -. Enqueue Async Jobs .-> Hangfire_Server
    Hangfire_Server --> SQL_DB
    Hangfire_Server -. Async Sync / Cache Warm .-> Redis_Service
    Hangfire_Server -. Sync Syllabus .-> FastAPI_App
    MVC_Controllers <== REST HTTP / JSON ==> FastAPI_App
    FastAPI_App --> RAG_Engine
    RAG_Engine <--> Vector_DB
    RAG_Engine <--> LLM_Provider
```

---

## 2. Role-Based Workflows & Code Execution Paths

EduPulse provides strict role-based separation across 4 distinct user profiles: **Guest/Visitor**, **Student**, **Instructor**, and **System Admin**.

### 2.1. Guest / Visitor Workflow
* **Capabilities:** Explore course catalog, filter categories, view pricing, test AI course recommendations, view public syllabus, register/login.
* **Security Restriction:** Cannot access classroom player, cannot take quizzes, cannot download materials, cannot access instructor/admin consoles.

```mermaid
sequenceDiagram
    autonumber
    actor Guest as Guest / Visitor
    participant CC as CoursesController
    participant RS as RedisCacheService
    participant CR as CourseRepository
    participant DB as SQL Server (Practice)
    participant AI as Python AI (Port 8000)

    Guest->>CC: GET /Courses/Index (Category filters)
    CC->>RS: Get("catalog:courses:filtered:...")
    alt Cache Hit
        RS-->>CC: Return cached Course List (0ms DB load)
    else Cache Miss
        CC->>CR: GetAllCourses(category, level)
        CR->>DB: EXEC LMS.sp_GetAllCourses
        DB-->>CR: Course Rows
        CR-->>CC: List<Course>
        CC->>RS: Set("catalog:courses:...", 15 mins)
    end
    CC-->>Guest: Render Catalog View

    Guest->>CC: POST /Courses/AiRecommend (Skills/Goals)
    CC->>AI: POST /api/advisor/recommend
    AI-->>CC: Recommended Courses + Match Scores
    CC-->>Guest: Display Tailored Recommendation Cards
```

---

### 2.2. Student Workflow
* **Capabilities:** Enroll in free/paid courses, checkout via simulated gateway, view interactive lessons, read lecture PDFs via secure canvas reader, take end-of-lesson quizzes, submit assignments, query Socratic AI assistant, download completion certificates.
* **Security & DRM:** Content download protection active (no right-click, no `Ctrl+S`, `controlsList="nodownload"` on video, dynamic forensic student watermark).

```mermaid
sequenceDiagram
    autonumber
    actor Student
    participant PC as PaymentController
    participant CC as CoursesController
    participant HF as Hangfire Server
    participant DB as SQL Server
    participant Guard as DRM Media Guard
    participant AI as RAG Assistant (api.py)

    Student->>PC: POST /Payment/Checkout (CourseId, PaymentDetails)
    PC->>DB: EXEC LMS.sp_ProcessPayment + sp_EnrollCourse
    PC->>HF: Enqueue(() => BackgroundJobService.ProcessEnrollmentPostActions())
    Note over HF: Sends welcome packet, invalidates catalog cache, pre-warms course details
    PC-->>Student: Payment Success & Redirect to /Courses/Learn/{id}

    Student->>CC: GET /Courses/Learn/{id}?lesson=1
    CC->>DB: Verify Active Enrollment
    CC-->>Student: Render Classroom View (Learn.cshtml)
    
    Note over Student,Guard: Guard blocks right-click, video download menu & keyboard save
    Student->>AI: Chat Query ("Explain Concurrent Rendering")
    AI-->>Student: Grounded Socratic Explanation + Slide Citations
```

---

### 2.3. Instructor Workflow
* **Capabilities:** Complete professional KYC profile, create new courses, manage modular syllabus, upload lecture slides (PDF/Video), generate multiple-choice quizzes with Gemini AI, review student assignment submissions, grade homework with rubric scores and feedback.
* **Verification Gate:** Newly registered instructors require System Admin verification (`IsApproved == 1`) before courses can be published.

```mermaid
sequenceDiagram
    autonumber
    actor Instructor
    participant IC as InstructorController
    participant IR as InstructorRepository
    participant DB as SQL Server
    participant AI as Python AI (Port 8000)

    Instructor->>IC: GET /Instructor/Profile
    IC->>IR: GetInstructorProfile(userId)
    IR->>DB: EXEC LMS.sp_GetInstructorProfile
    DB-->>IR: Profile Data
    IR-->>IC: InstructorProfileViewModel
    IC-->>Instructor: Render Studio Profile

    Instructor->>IC: POST /Instructor/CreateModule (Title, Markdown, PDF Upload)
    IC->>IR: AddCourseModule(module)
    IC->>AI: POST /api/instructor/quiz/generate-from-pdf
    AI-->>IC: 3 Pedagogical Questions + Explanations
    IC->>DB: Seed Module Quiz Questions
    IC-->>Instructor: Module Created & Quiz Generated
```

---

### 2.4. System Admin Workflow
* **Capabilities:** View platform analytics, approve/reject pending instructor applications, inspect instructor KYC credentials (degree, portfolio, ID, digital signature), audit payments and revenue, inspect Redis health and Hangfire job dashboard.

```mermaid
sequenceDiagram
    autonumber
    actor Admin
    participant AC as AdminController
    participant DB as SQL Server
    participant HF as Hangfire Dashboard

    Admin->>AC: GET /Admin/VerifySellers
    AC->>DB: EXEC LMS.sp_GetPendingInstructors
    DB-->>AC: List of unverified instructors
    AC-->>Admin: Verification Queue Table

    Admin->>AC: POST /Admin/ReviewInstructor (UserId, Action="Approve", Feedback)
    AC->>DB: EXEC LMS.sp_ReviewInstructor
    DB-->>AC: Success
    AC-->>Admin: Instructor Activated Notification

    Admin->>HF: GET /hangfire
    HF-->>Admin: Real-time Job Queue Latency, Retry Graph, Scheduled Pre-warms
```

---

## 3. Comprehensive File Catalog & Location Map

### 3.1. ASP.NET MVC Core Tier (`WebApplication1/`)

| File Path | Layer | Responsibility / Usage |
| :--- | :--- | :--- |
| `Controllers/CoursesController.cs` | Controller | Catalog filtering, course details, classroom player (`Learn`), benchmark endpoint (`/Courses/Benchmark`), and AI recommender proxy. |
| `Controllers/InstructorController.cs`| Controller | Instructor studio: profile onboarding, course drafting, curriculum module editor, AI quiz generator bridge, and student assignment grading. |
| `Controllers/AdminController.cs` | Controller | Platform governance: instructor KYC verification, course publication audit, revenue analytics. |
| `Controllers/AccountController.cs` | Controller | Authentication, registration, session management, and role-based redirect routing. |
| `Controllers/PaymentController.cs` | Controller | Checkout simulation, invoice generation, tax invoice PDF rendering, and enrollment processing. |
| `Controllers/ChatbotController.cs` | Controller | Reverse-proxy gateway routing student and instructor floating widget messages to FastAPI (Port 8000). |
| `Services/RedisCacheService.cs` | Infrastructure | High-performance wrapper over `StackExchange.Redis` for catalog caching, course details caching, pattern eviction, and health diagnostics. |
| `Services/BackgroundJobService.cs` | Infrastructure | Hangfire task runner: catalog cache pre-warming, post-enrollment email dispatch, and asynchronous course-to-AI sync. |
| `Services/CloudinaryService.cs` | Infrastructure | Multipart media uploader for instructor profile photos and digital signatures with graceful local disk fallback. |
| `Repositories/CourseRepository.cs` | Data Access | ADO.NET data access executing stored procedures for courses, categories, enrollments, reviews, and progress tracking. |
| `Repositories/InstructorRepository.cs`| Data Access | Stored procedure client for instructor profiles, curriculum modules, student assignment submissions, and quiz attempts. |
| `Data/DbHelper.cs` | Data Access | Centralized ADO.NET helper executing parameterized SQL queries, commands, DataTables, and transactions. |
| `Data/DatabaseInitializer.cs` | Data Access | Startup database migration hook creating core LMS tables and default demo users if they do not exist. |
| `Startup.cs` | OWIN Startup | Registers Hangfire server, mounts `/hangfire` real-time management dashboard, and triggers standing recurring jobs. |
| `Web.config` | Configuration | Connection strings (`LmsConnection`), Cloudinary keys, assembly binding redirects, and HTTP static file handlers. |

---

### 3.2. Learning Views & Client Security Assets (`Views/` & `Scripts/`)

| File Path | Layer | Responsibility / Usage |
| :--- | :--- | :--- |
| `Views/Courses/Learn.cshtml` | View (Razor) | Classroom player view: PDF.js canvas viewer, direct HTML5 video stream, dynamic student watermark, and end-of-lesson quiz drawer. |
| `Views/Courses/Details.cshtml` | View (Razor) | Comprehensive course details page with curriculum syllabus accordion, protected cheatsheets list, and AI drawer prompt launchers. |
| `Views/Instructor/Modules.cshtml` | View (Razor) | Curriculum editor with module drag-order, PDF upload, study guide manager, and AI quiz trigger. |
| `Views/Instructor/ManageQuiz.cshtml` | View (Razor) | Instructor quiz manager allowing manual question authoring and one-click AI quiz generation from slide notes. |
| `Views/Instructor/Submissions.cshtml`| View (Razor) | Student assignment grading desk with modal document viewer, rubric sliders, and feedback form. |
| `Views/Shared/_Layout.cshtml` | Shared View | Master layout hosting role-specific navbars, global alerts, media guard scripts, and floating AI chat widget. |
| `Views/Shared/_ChatWidget.cshtml` | Shared View | Floating Socratic AI Teaching Assistant UI with markdown rendering, LaTeX math (KaTeX), citation pills, and prompt suggestion chips. |
| `Content/ep-media-guard.css` | Stylesheet | Digital rights protection CSS: disables text/media selection, styles forensic watermarks, and blanks view during print (`Ctrl+P`). |
| `Scripts/ep-media-guard.js` | Client Script | Digital rights protection JS: intercepts right-click on media, blocks `Ctrl+S`/`Ctrl+P`, disables image drag, overrides canvas export. |
| `Uploads/web.config` | IIS Config | Sets response headers on static files (`Content-Disposition: inline`, `X-Download-Options: noopen`, `Cache-Control: no-store`). |

---

### 3.3. Python RAG Assistant Microservice (`WebApplication1/Rag_Service/`)

| File Path | Role | Responsibility / Usage |
| :--- | :--- | :--- |
| `api.py` | FastAPI Core | REST microservice exposing `/api/chat`, `/api/instructor/quiz/generate-from-pdf`, `/api/advisor/recommend`, and health endpoints. |
| `rag_engine.py` | RAG Pipeline | Context assembler: query classification, RBAC filtering, citation generation, and system prompt orchestration. |
| `hybrid_search.py` | Search Engine | Reciprocal Rank Fusion (RRF) combiner uniting dense semantic vector search with sparse BM25 keyword matching. |
| `vector_store.py` | Vector Storage | ChromaDB persistent store management, chunk insertion, and Google Gemini embedding calculation (`gemini-embedding-001`). |
| `instructor_tools.py` | Instructor AI | Tools for automated quiz authoring from PDF slides, student assignment rubric evaluation, and course consistency auditing. |
| `course_recommender.py` | Recommendation | Semantic vector recommender matching student career goals to course curriculum modules using cosine similarity. |
| `security_guardrails.py` | Guardrails | Prompt injection detection, jailbreak suppression, toxic input filtering, and role-boundary validation. |
| `resilience.py` | Reliability | Sliding-window rate limiter (bot defense), semantic response cache (5ms repeated query returns), and tiered model cascading. |
| `ingest_study_materials.py` | Ingestion Script | Standalone pipeline indexing lecture slide PDFs into ChromaDB with course and module metadata tags. |
| `.env` | Configuration | Environment variables: `GEMINI_API_KEY`, `CHAT_MODEL`, `EMBEDDING_MODEL`, and `CHROMA_DB_DIR`. |
