# EduPulse LMS: Tech Stack Justifications & Inter-Service Communication Protocol

> **System Target:** Comprehensive Technology Stack Rationale, Service Responsibilities & Cross-Tier Communication Protocols.

---

## 1. Complete Technology Stack Matrix

```mermaid
flowchart TD
    subgraph Frontend_Tech ["Client & Presentation Tier"]
        BS5["Bootstrap 5.3 + Bootstrap Icons"]
        PDFJS["Mozilla PDF.js 3.11 (Canvas-Based PDF Rendering)"]
        KATEX["KaTeX (LaTeX Math Rendering)"]
        MARKED["Marked.js (Markdown Rendering)"]
        DRM_JS["ep-media-guard.js & ep-media-guard.css"]
    end

    subgraph Backend_Web_Tech ["Core Web & Asynchronous Processing Tier"]
        NET["ASP.NET MVC 5 (.NET Framework 4.7.2, C#)"]
        OWIN["Microsoft OWIN 4.2.2 Middleware"]
        HANGFIRE["Hangfire 1.8.14 (In-Process Job Processing Server)"]
        STACK_REDIS["StackExchange.Redis 2.6.122 Client"]
        ADONET["ADO.NET (DbHelper + Parameterized Stored Procedures)"]
    end

    subgraph Persistence_Tech ["Storage & Distributed State Tier"]
        MSSQL["Microsoft SQL Server 2022 (Practice DB)"]
        REDIS["Redis Server 5.0.14+ (Port 6379, In-Memory KV Store)"]
        CLOUDINARY["Cloudinary Media API (Profiles & Signatures)"]
    end

    subgraph AI_Subsystem_Tech ["AI & Semantic Retrieval Subsystem"]
        PYTHON["Python 3.12 + FastAPI + Uvicorn"]
        CHROMA["ChromaDB 0.5+ (Persistent Vector Database)"]
        BM25["Rank-BM25 (Sparse Lexical Keyword Index)"]
        PYPDF["PyPDF 4.0+ (Binary PDF Stream Extraction)"]
        GEMINI["Google Gemini API (Flash-Lite, Flash, Embeddings)"]
    end

    Frontend_Tech <== "HTTPS / JSON / Razor" ==> Backend_Web_Tech
    Backend_Web_Tech <== "T-SQL / TCP Port 1433" ==> MSSQL
    Backend_Web_Tech <== "RESP Protocol / Port 6379" ==> REDIS
    Backend_Web_Tech <== "REST HTTPS / JSON" ==> CLOUDINARY
    Backend_Web_Tech <== "HTTP REST / Port 8000" ==> AI_Subsystem_Tech
    AI_Subsystem_Tech <== "HTTPS / REST" ==> GEMINI
    AI_Subsystem_Tech <== "Disk IPC" ==> CHROMA
```

---

## 2. Technology Selection Rationale ("Why This Stack?")

### 2.1. ASP.NET MVC 5 (.NET Framework 4.7.2 & C#)
* **Role:** Serves user sessions, authentication, server-rendered Razor views, and enterprise business logic.
* **Why Selected:**
  * **Strict Typing & Enterprise Stability:** C# provides compile-time safety, robust LINQ data modeling, and high performance.
  * **Rock-Solid Security Pipeline:** Built-in protection against Cross-Site Request Forgery (`[ValidateAntiForgeryToken]`), Cross-Site Scripting (HTML-encoded Razor syntax), and SQL Injection (ADO.NET parameter binding).
  * **OWIN Extensibility:** Easily embeds middleware components like Hangfire dashboards without needing an external web server process.

### 2.2. Microsoft SQL Server 2022 (T-SQL & Stored Procedures)
* **Role:** Single source of truth for platform users, course catalogs, enrollment progress, payment invoices, and student quiz attempts.
* **Why Selected:**
  * **ACID Guarantees:** Ensures zero double-enrollment or lost payments under high checkout concurrency through transactional locks (`BEGIN TRANSACTION`).
  * **Stored Procedure Isolation:** Keeping queries in stored procedures (`LMS.sp_*`) prevents dynamic SQL injection vulnerabilities, optimizes query execution plans in SQL cache, and allows schema modifications without recompiling the web application.

### 2.3. Redis Server (StackExchange.Redis)
* **Role:** High-speed in-memory caching for course catalog queries, category lists, course detail payloads, and rate limits.
* **Why Selected:**
  * **Sub-Millisecond Read Latency:** Bypasses SQL Server for 95% of catalog browse requests, serving course lists in **< 2ms**.
  * **Memory-Efficient Key-Value Serialization:** JSON-serialized data structures with customizable Time-To-Live (TTL) policies and pattern-based cache eviction (`catalog:courses:*`).

### 2.4. Hangfire 1.8.14 (Background Processing Server)
* **Role:** In-process asynchronous task distributor and recurring cron scheduler.
* **Why Selected:**
  * **No Separate Windows Service Required:** Runs directly inside the IIS application pool (`Clr4IntegratedAppPool`).
  * **Persistent SQL Storage:** Job state is saved in `HangFire.*` SQL tables. If IIS restarts, in-flight jobs are never lost and automatically resume.
  * **Built-In Live Dashboard:** Visual inspection of job queues, retry attempts, and latency metrics directly at `/hangfire`.

### 2.5. Python 3.12 + FastAPI + ChromaDB (Port 8000)
* **Role:** Autonomous AI microservice executing document parsing, vector retrieval, and LLM orchestration.
* **Why Selected:**
  * **Rich AI Ecosystem:** Python provides native bindings for scientific libraries (`numpy`, `pypdf`, `rank_bm25`, `chromadb`, `google-generativeai`).
  * **Asynchronous High Concurrency (FastAPI):** Built on Starlette and Pydantic, FastAPI handles concurrent student chat streams with non-blocking async event loops.
  * **Serverless Local Vector DB (ChromaDB):** Persistent vector embeddings saved directly to disk (`./chroma_data`), eliminating the need for expensive third-party managed vector cloud providers.

### 2.6. Google Gemini Models (Flash-Lite & Flash)
* **Role:** Embeddings computation, Socratic tutoring, and quiz generation.
* **Why Selected:**
  * **Cost-Efficiency & Latency:** `gemini-3.5-flash-lite` provides sub-second conversational latency and costs a fraction of GPT-4o, making high-volume student Q&A financially sustainable.
  * **Generous Free Tier:** Google AI Studio allows 15 RPM / 1M TPM free, enabling full testing without infrastructure expenditure.

---

## 3. Inter-Service Communication Protocols

EduPulse employs standardized protocols between each tier to ensure fault isolation and prevent cascading failures:

```mermaid
sequenceDiagram
    autonumber
    participant Browser as Client Browser
    participant MVC as ASP.NET MVC (Port 44301 / 57794)
    participant Redis as Redis (Port 6379)
    participant SQL as SQL Server (Port 1433)
    participant HF as Hangfire Worker
    participant AI as Python FastAPI (Port 8000)
    participant Gemini as Google Cloud API

    Note over Browser,MVC: 1. Student Browsing & Catalog Request
    Browser->>MVC: HTTP GET /Courses/Index?category=Frontend
    MVC->>Redis: RESP: GET "catalog:courses:filtered:Frontend"
    alt Cache Hit
        Redis-->>MVC: Return JSON Payload (< 2ms)
    else Cache Miss
        MVC->>SQL: TDS Protocol: EXEC LMS.sp_GetAllCourses
        SQL-->>MVC: Tabular Recordset (60ms)
        MVC->>Redis: RESP: SETEX "catalog:courses:filtered:Frontend" (TTL: 900s)
    end
    MVC-->>Browser: HTTP 200 HTML Stream

    Note over Browser,MVC: 2. Checkout & Asynchronous Job Trigger
    Browser->>MVC: HTTP POST /Payment/Checkout (CourseId=1)
    MVC->>SQL: TDS Protocol: BEGIN TRAN -> EXEC sp_ProcessPayment -> COMMIT
    SQL-->>MVC: Invoice #INV-1001 Generated
    MVC->>HF: Enqueue(() => ProcessEnrollmentPostActions(userId=3, courseId=1))
    HF->>SQL: INSERT INTO HangFire.Job (State='Enqueued')
    MVC-->>Browser: HTTP 302 Redirect to /Courses/Learn/1

    Note over HF,AI: 3. Background Worker Execution (Asynchronous)
    HF->>SQL: Fetch Enqueued Job
    HF->>Redis: DEL "catalog:courses:all"
    HF->>AI: HTTP POST /api/knowledge/sync (CourseId=1)
    AI-->>HF: Sync Acknowledged (200 OK)

    Note over Browser,AI: 4. Classroom Socratic Tutoring Interaction
    Browser->>MVC: HTTP POST /Chatbot/Ask (Query: "Explain React 18 Fiber")
    MVC->>AI: HTTP POST http://127.0.0.1:8000/api/chat
    AI->>Gemini: HTTPS POST /v1beta/models/gemini-embedding-001
    Gemini-->>AI: Vector Array [0.012, -0.045, ...]
    AI->>AI: ChromaDB Vector Query + BM25 Sparse Search + RRF Re-rank
    AI->>Gemini: HTTPS POST /v1beta/models/gemini-3.5-flash-lite (Prompt + Context)
    Gemini-->>AI: Socratic Markdown Response + Citations
    AI-->>MVC: JSON { answer: "...", citations: [...] }
    MVC-->>Browser: JSON Stream Rendered into Chat Widget
```

---

## 4. Communication Channel Security & Error Fallback Matrix

| Channel | Protocol | Payload Type | Fallback Policy on Connection Outage |
| :--- | :--- | :--- | :--- |
| **Browser $\rightarrow$ MVC** | HTTPS / TLS 1.2+ | HTML / Form Data / JSON | Browser displays standard user-friendly error page; static assets served from cache. |
| **MVC $\rightarrow$ SQL Server** | TDS (TCP 1433) | Parameterized ADO.NET | Connection retry logic; database failure logs to Windows Event Viewer and `Debug.WriteLine`. |
| **MVC $\rightarrow$ Redis** | RESP (TCP 6379) | Serialized JSON Strings | `RedisCacheService.IsConnected` switches to `false`; application falls back directly to SQL Server seamlessly. |
| **MVC $\rightarrow$ Python AI** | HTTP REST (TCP 8000) | JSON (`ChatRequestDto`) | Returns polite fallback message: *"The AI Assistant is currently undergoing scheduled indexing. Please consult lecture slides directly."* |
| **Python $\rightarrow$ Gemini API** | HTTPS REST (TCP 443) | JSON (REST Transport) | Automatically cascades: Tier 1 (`gemini-3.5-flash-lite`) $\rightarrow$ Tier 2 (`gemini-3.8-flash`) $\rightarrow$ Tier 3 (Local extractive notes). |
| **MVC $\rightarrow$ Cloudinary** | HTTPS REST | Multipart `form-data` | `CloudinaryService` falls back to saving files locally into `~/Uploads/profiles/` or `~/Uploads/signatures/`. |
