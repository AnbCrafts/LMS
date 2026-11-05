# EduPulse LMS — Complete Project Concept, Architecture & UI/UX Specification

> **Purpose of this Document**:  
> This document serves as the single source of truth for the **EduPulse Learning Management System (LMS)**. It outlines the core business concept, technical architecture, user personas, end-to-end workflows, AI/RAG capabilities, and strict UI/UX design rules.  
> **Use this document as context for GitHub Copilot, Gemini, or senior UI/UX designers to receive high-craft, professional design guidance without generic AI clichés.**

---

## 1. Executive Summary & Project Vision

**EduPulse** is an enterprise-grade, full-lifecycle Learning Management System and EdTech Marketplace. It unites three core pillars:
1. **Curriculum-First Learning**: Structured, high-retention video and document learning environments with interactive quizzes, assignment submissions, verified certifications, and a course-grounded Socratic AI teaching assistant.
2. **Creator Economy for Educators**: An accredited instructor marketplace where vetted experts author courses, upload study slides, set pricing, auto-generate assessments, evaluate submissions with AI copilot assistance, and keep **90% net revenue**.
3. **Platform Governance & Marketplace Integrity**: A 10% platform fee model with automated invoice generation, rigorous instructor accreditation (reviewing credentials, resumes, and digital signatures), and a global financial ledger.

---

## 2. Technical Stack & Infrastructure

- **Web Application Backend**: ASP.NET MVC 5 (.NET Framework 4.7.2, C#)
- **Database**: Microsoft SQL Server (LocalDB/Express, relational schema `LMS.*`)
- **Frontend Layer**: Razor View Engine (`.cshtml`), Bootstrap 5, Bootstrap Icons (`bi-*`), Custom Modern CSS (`Content/Site.css`)
- **AI & RAG Microservice**: Python FastAPI daemon (port 8000), Google Gemini APIs (`gemini-3.5-flash-lite`, `gemini-embedding-001`)
- **Vector Database**: Persistent local ChromaDB (`lms_course_knowledge`, HNSW Cosine Index) paired with BM25 Reciprocal Rank Fusion (RRF) for hybrid retrieval
- **Document Ingestion**: PyMuPDF / pdfplumber for PDF slide chunking, metadata extraction, and vectorization

---

## 3. The Three User Personas & End-to-End Workflows

### Persona A: Learner / Student
* **Goal**: Discover career-advancing tech courses, learn at their own pace, submit assignments, test comprehension with quizzes, and earn verifiable certificates.
* **Key Workflows**:
  1. **Course Discovery**:
     - Browse catalog with real-time category filtering, difficulty level toggles, search, and an AI Course Matcher / Pathway Advisor.
     - Inspect course landing pages: instructor credentials, syllabus accordions, prerequisites, reviews, and pricing.
  2. **Enrollment & Billing**:
     - Checkout flow, mock payment gateway, immediate enrollment activation, and printable PDF tax invoice receipt.
  3. **Learning Dashboard**:
     - Enrolled courses grid with dynamic progress percentages, assignment submission statuses (Pending / Approved / Action Required), and quiz scores.
  4. **Active Learning Classroom (`/Courses/Learn/{id}`)**:
     - Dual-column workspace: primary media player (HTML5 MP4 or embedded YouTube) and embedded PDF study guide reader.
     - Expandable curriculum sidebar with sequential lecture checkboxes and progress persistence.
     - Assignment submission drawer supporting rich markdown text and project file uploads.
     - Floating Socratic AI Assistant drawer grounded strictly in the current course's uploaded materials.
  5. **Assessment & Credentialing**:
     - Interactive MCQ quizzes with real-time scoring and retake logic.
     - Verifiable Course Completion Certificate featuring an official credential ID, completion date, course grade summary, and the instructor's digital signature.

---

### Persona B: Instructor / Course Creator
* **Goal**: Build high-value courses, publish multimedia curriculums, engage students, grade assignments efficiently, and earn income.
* **Key Workflows**:
  1. **Instructor Studio & Dashboard (`/Instructor/Index`)**:
     - Financial KPI metrics: Gross Course Revenue, Platform Fee Deductions (10%), and Net Take-Home Earnings (90%).
     - **Student Learning Friction & Confusion Heatmap**: An analytics card highlighting the specific concepts and modules students ask the AI assistant about most frequently.
     - Course management table with status toggles (Draft, Published) and quick actions.
  2. **Accreditation & 90% Profile Gate (`/Instructor/Profile`)**:
     - Strict verification requirement: Instructors must achieve $\ge 90\%$ profile completeness (bio, professional credentials, payout bank/UPI info, digital signature upload) before courses can be published.
     - Status tracking screen (`/Instructor/ApplicationStatus`) displaying admin reviews and revision notes.
  3. **Course & Curriculum Builder**:
     - Course creation wizard (`/Instructor/Create`, `/Instructor/Edit`): title, description, category, level, pricing, cover image.
     - Module & lecture manager (`/Instructor/Modules`): add modules, attach video URLs, upload PDF slide decks (up to 100 MB with automated background vectorization for RAG), and define assignment rubrics.
  4. **Assessment & Grading Workbench**:
     - Quiz Builder (`/Instructor/ManageQuiz`): manual MCQ builder + **1-Click AI Quiz Generator** that reads uploaded course PDFs and outputs verified MCQs.
     - Submissions Backlog (`/Instructor/Submissions`): review student submitted code/files, enter grades (0–100), write feedback, or click **"AI Auto-Evaluate"** for rubric-grounded scoring suggestions.
  5. **Financial Earnings Ledger (`/Payment/InstructorInvoices`)**:
     - Transaction-by-transaction breakdown showing gross amount, 10% platform fee, and 90% net payout statements.

---

### Persona C: Administrator / Platform Governance
* **Goal**: Maintain platform quality, review instructor accreditations, audit marketplace financial health, and monitor transactions.
* **Key Workflows**:
  1. **Executive Management Center (`/Admin/Index`)**:
     - Platform Gross Merchandise Volume (GMV), Total 10% Platform Commission Revenue, pending seller reviews count, and recent ledger activity.
  2. **Seller Verification Center (`/Admin/VerifySellers`)**:
     - Filterable queues: `Pending Review`, `Action Required`, and `Approved`.
  3. **Accreditation Review Workbench (`/Admin/VerifyInstructor`)**:
     - Dossier inspection: examine applicant's resume, credentials, experience, and uploaded digital signature.
     - Decision actions: Approve application, Reject, or Request Revisions with custom actionable feedback.
  4. **Master Financial Ledger (`/Payment/AdminInvoices`)**:
     - Platform-wide audit table tracking every student purchase, student name, instructor name, course title, total price, 10% platform cut, and 90% instructor settlement.

---

## 4. Artificial Intelligence & RAG Capabilities (Under the Hood)

EduPulse features deep AI integrations that serve practical pedagogical and operational purposes:
1. **Course-Scoped Socratic Tutor**: An AI assistant available inside the classroom (`/Courses/Learn/{id}`) that only answers questions based on the course's ingested slides/transcripts. Adheres to Socratic teaching principles—refuses to write homework solutions directly; instead provides guided hints and slide citations (`[Source: Module 1, Page 4]`).
2. **Global Multi-Course AI Assistant**: Available on the student dashboard; dynamically retrieves context only from courses the student has paid for and enrolled in.
3. **AI Assignment Evaluator**: Evaluates student code and written submissions against the instructor's rubric, providing constructive criteria feedback and a provisional score.
4. **AI Quiz Generator**: Reads uploaded PDF lecture slides and automatically drafts 4-option multiple-choice questions with correct answer keys.
5. **AI Course Matcher & Pathway Advisor**: Recommends personalized courses based on a user's career goals and existing skill set.
6. **Concept Confusion Heatmap**: Synthesizes student queries to alert instructors about curriculum bottlenecks.

---

## 5. Non-Negotiable UI/UX Design Rules & Philosophy

To ensure the user interface achieves **tier-1 product craftsmanship** (similar to Linear, Stripe, Maven, or Apple developer portals), all UI proposals must adhere strictly to these three rules:

### Rule 1: Eliminate the "AI-Touched" Essence & Cliché Patterns
- 🚫 **No Cliché Gradients**: Strictly avoid the ubiquitous purple-to-indigo or neon-cyan mesh gradients on headers, text, and cards.
- 🚫 **No Sparkle / Robot Tropes**: No sparkle stars (`bi-stars`), magic wand icons, floating robot badges with "AI Online 🟢", or neon glowing borders.
- 🚫 **No Tech-Babble Filler**: Avoid robotic buzzwords like *"Empower your synergies with next-gen AI-driven learning"* or *"Unlock infinite possibilities"*. Use crisp, human, functional copy (e.g., *"Master Full-Stack Engineering with Industry Mentors"*).
- ✅ **Bespoke Typography**: Utilize clean, professional typography stacks (e.g. system geometric sans `-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto`, or refined sans like Geist / Plus Jakarta Sans) with deliberate hierarchy, tight headings (`letter-spacing: -0.02em`), and legible body copy.
- ✅ **Subtle Micro-Elevation**: Use crisp 1px borders (`#e2e8f0`, `#e5e7eb`, `#1e293b10`) and multi-stop ambient shadows (`rgba(15, 23, 42, 0.04)`) rather than heavy neon drop shadows.

### Rule 2: No Generalized / Cookie-Cutter Theme Design
- 🚫 **No Generic Bootstrap Templates**: Reject cookie-cutter "3 identical cards with circular pastel icon backgrounds" repeated on every page.
- 🚫 **No Monotonous Grids**: Don't use the same standard card layout for every entity. A course card, an analytics card, a submission drawer, and a curriculum module must each have bespoke layouts tailored to their specific function.
- ✅ **Purposeful Layout Architecture**:
  - **Learning Classroom**: Distraction-free, ergonomic layout optimized for sustained video viewing and PDF reading.
  - **Instructor Studio**: High-density operational cockpit featuring clear KPI numbers, progress indicators, and actionable tables.
  - **Admin Ledger**: Clean data-dense tables with subtle zebra striping, monospace transaction IDs, and status pills.

### Rule 3: Zero Dummy Data Stuffing & Remove Test Clutter
- 🚫 **No Fabricated Statistics**: Never hardcode fake figures like *"1,250+ Active Students"*, *"98% Completion Rate"*, or *"4.9 Star Rating"* unless they are bound to real dynamic database values.
- 🚫 **No Test-Mode Artifacts**: Remove all testing clutter, such as "Demo Quick Persona Switcher" dropdowns in the navbar or "One-Click Instant Demo Login" cards on the login page.
- ✅ **Graceful Empty States**: If an instructor has 0 courses or a student has 0 submissions, render an elegant, helpful zero-state card with a clear call-to-action (e.g., *"No active courses yet. Browse the catalog to start learning."*), never fake placeholder items.

---

## 6. Complete UI Screen & View Inventory

When designing or revising screens, refer to the exact file paths:

### Global & Shared
- `Views/Shared/_Layout.cshtml` — Root responsive shell, top sticky navbar, dynamic role navigation, user avatar menu, footer.
- `Views/Shared/_ChatWidget.cshtml` — Slide-out RAG assistant drawer with role-adaptive modes.
- `Views/Shared/Error.cshtml` — Friendly application error screen.
- `Content/Site.css` — Global CSS variables, utility classes, typography, and card components.
- `Views/Account/Login.cshtml` — Clean, professional credentials sign-in.
- `Views/Account/Register.cshtml` — Student vs Instructor registration selector.
- `Views/Account/Profile.cshtml` — Account settings and personal profile.
- `Views/Home/Index.cshtml` — Public homepage and platform introduction.
- `Views/Home/About.cshtml` & `Contact.cshtml` — Company story and support portal.

### Student / Learner Views
- `Views/Dashboard/Index.cshtml` — Student dashboard, active course progress, quiz history, submission feedback.
- `Views/Courses/Index.cshtml` — Course catalog, search, category filters, difficulty tags, AI Pathway Advisor.
- `Views/Courses/Details.cshtml` — Course landing page, syllabus breakdown, instructor accreditation, enroll CTA.
- `Views/Courses/Learn.cshtml` — Video player, PDF viewer, module checklist, assignment submission box.
- `Views/Courses/Certificate.cshtml` — Verified certificate of completion with print/PDF export.
- `Views/Payment/Checkout.cshtml` — Order summary and payment options.
- `Views/Payment/StudentInvoices.cshtml` — Billing history and receipts.
- `Views/Payment/Invoice.cshtml` — Printable tax invoice.

### Instructor Views
- `Views/Instructor/Index.cshtml` — Studio dashboard, financial KPIs, Confusion Heatmap, course list.
- `Views/Instructor/Profile.cshtml` — 90% completion profile builder, payout details, signature upload.
- `Views/Instructor/ApplicationStatus.cshtml` — Verification status tracking with admin feedback.
- `Views/Instructor/Create.cshtml` & `Edit.cshtml` — Course creation wizard and editor.
- `Views/Instructor/Modules.cshtml` — Curriculum builder, video/PDF upload, assignment prompts.
- `Views/Instructor/ManageQuiz.cshtml` — Quiz builder & AI Quiz Generator.
- `Views/Instructor/Submissions.cshtml` — Grading backlog, file inspection, AI Auto-Evaluator.
- `Views/Instructor/Certificate.cshtml` — Certificate preview template.
- `Views/Payment/InstructorInvoices.cshtml` — Sales statements and 90% payout ledger.

### Admin Views
- `Views/Admin/Index.cshtml` — Platform executive dashboard and GMV metrics.
- `Views/Admin/VerifySellers.cshtml` — Instructor accreditation review queue.
- `Views/Admin/VerifyInstructor.cshtml` — Comprehensive instructor dossier review & approval form.
- `Views/Payment/AdminInvoices.cshtml` — Platform master financial ledger.

---

## 7. How to Prompt External LLMs (Copilot / Gemini) with this Specification

When asking Copilot or Gemini for UI redesign suggestions, provide this prompt template:

```markdown
I am redesigning the UI for EduPulse, a modern enterprise-grade LMS built with ASP.NET MVC 5 and Bootstrap 5.
Refer to the attached "PROJECT_CONCEPT_AND_UI_SPECIFICATION.md" for complete project context.

Strict Design Constraints:
1. NO AI-touched clichés (no purple gradients, no sparkle icons, no robot badges, no buzzword fluff).
2. NO generalized theme design or cookie-cutter Bootstrap 3-card templates. Design bespoke, professional components comparable to Linear, Stripe, or Maven.
3. NO hardcoded dummy data (no fake student numbers or fake ratings) and NO test-purpose artifacts.
4. Output clean Razor (.cshtml) markup and custom CSS with proper @Model and @ViewBag bindings.

Current Task:
[Specify the page or component you want to redesign, e.g., "Redesign the Landing Page (Views/Home/Index.cshtml)"]
```
