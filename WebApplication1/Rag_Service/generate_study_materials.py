"""
generate_study_materials.py
Generates professional course study materials / lecture note PDFs using ReportLab.
Saved into D:/Anubhaw/Projects/Assignments/LMSPractice/WebApplication1/Uploads/CourseMaterials/
"""

import os
from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, HRFlowable

OUTPUT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Uploads", "CourseMaterials"))
os.makedirs(OUTPUT_DIR, exist_ok=True)

def create_study_guide_pdf(filename: str, title: str, course_name: str, instructor: str, sections: list):
    filepath = os.path.join(OUTPUT_DIR, filename)
    doc = SimpleDocTemplate(
        filepath,
        pagesize=letter,
        rightMargin=40,
        leftMargin=40,
        topMargin=40,
        bottomMargin=40
    )

    styles = getSampleStyleSheet()
    
    # Custom styles
    title_style = ParagraphStyle(
        'DocTitle',
        parent=styles['Heading1'],
        fontName='Helvetica-Bold',
        fontSize=20,
        leading=24,
        textColor=colors.HexColor('#1e293b')
    )
    
    meta_style = ParagraphStyle(
        'DocMeta',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=10,
        leading=14,
        textColor=colors.HexColor('#64748b')
    )
    
    h2_style = ParagraphStyle(
        'SectionH2',
        parent=styles['Heading2'],
        fontName='Helvetica-Bold',
        fontSize=13,
        leading=17,
        textColor=colors.HexColor('#4338ca'),
        spaceBefore=12,
        spaceAfter=6
    )
    
    body_style = ParagraphStyle(
        'BodyDark',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=10,
        leading=14,
        textColor=colors.HexColor('#334155'),
        spaceAfter=8
    )
    
    code_style = ParagraphStyle(
        'CodeSnippet',
        parent=styles['Normal'],
        fontName='Courier',
        fontSize=8.5,
        leading=11,
        textColor=colors.HexColor('#0f172a'),
        backColor=colors.HexColor('#f1f5f9'),
        borderPadding=6,
        spaceBefore=4,
        spaceAfter=8
    )

    elements = []
    
    # Header block
    elements.append(Paragraph(f"<b>{course_name}</b>", meta_style))
    elements.append(Paragraph(title, title_style))
    elements.append(Paragraph(f"Official Study Guide &bull; Instructor: {instructor} &bull; EduPulse Academy", meta_style))
    elements.append(Spacer(1, 10))
    elements.append(HRFlowable(width="100%", thickness=1.5, color=colors.HexColor('#4f46e5'), spaceAfter=15))
    
    for sec_title, sec_paragraphs, code in sections:
        elements.append(Paragraph(sec_title, h2_style))
        for p in sec_paragraphs:
            elements.append(Paragraph(p, body_style))
        if code:
            elements.append(Paragraph(code.replace("\n", "<br/>").replace(" ", "&nbsp;"), code_style))
        elements.append(Spacer(1, 6))

    elements.append(HRFlowable(width="100%", thickness=0.5, color=colors.HexColor('#cbd5e1'), spaceBefore=15, spaceAfter=8))
    elements.append(Paragraph("Copyright &copy; 2026 EduPulse LMS. All materials protected for enrolled student study use only.", meta_style))

    doc.build(elements)
    print(f"Generated: {filepath} ({os.path.getsize(filepath):,} bytes)")
    return filepath

if __name__ == "__main__":
    # PDF 1: React 18 & Redux Architecture
    create_study_guide_pdf(
        "Course1_React18_Architecture_Cheatsheet.pdf",
        "React 18 & Concurrent Rendering Architecture Guide",
        "Course 1: Modern React 18 & Redux Toolkit Masterclass",
        "A. Sharma (Staff Frontend Architect)",
        [
            ("1. Understanding Concurrent Mode & Fiber Architecture", [
                "React 18 introduces concurrent rendering, allowing React to prepare multiple versions of your UI concurrently behind the scenes. This prevents expensive UI re-renders from freezing user interactions.",
                "The Fiber tree is a mutable data structure where each node represents a component and its state. Interruptible rendering splits work into discrete chunks that yield control back to the browser event loop."
            ], "const [isPending, startTransition] = useTransition();\n\nfunction handleSearch(term) {\n  setQuery(term); // Urgent state update\n  startTransition(() => {\n    setFilteredResults(computeExpensiveFilter(term)); // Transition\n  });\n}"),
            
            ("2. State Management with Redux Toolkit (RTK)", [
                "Redux Toolkit streamlines immutable state updates using Immer internally. Slices encapsulate initial state, reducers, and auto-generated action creators.",
                "Selectors should be memoized using createSelector to avoid unnecessary recomputations when non-relevant branches of state change."
            ], "export const counterSlice = createSlice({\n  name: 'counter',\n  initialState: { value: 0 },\n  reducers: {\n    increment: (state) => { state.value += 1; },\n    addAmount: (state, action) => { state.value += action.payload; }\n  }\n});"),
            
            ("3. Performance Optimization Rules", [
                "Always wrap expensive callbacks passed to memoized children in useCallback.",
                "Use React.memo only when profiling demonstrates measurable re-render bottlenecks.",
                "Avoid instantiating inline arrow functions or object literals directly inside render JSX if the component is wrapped with React.memo."
            ], None)
        ]
    )

    # PDF 2: ASP.NET MVC 5 & Web API Architecture
    create_study_guide_pdf(
        "Course2_ASPNET_MVC5_Enterprise_Architecture.pdf",
        "ASP.NET MVC 5 Enterprise Architecture & Web API Reference",
        "Course 2: Enterprise ASP.NET MVC 5 & SQL Server",
        "Dr. Vikram Sen (Principal Cloud Solutions Architect)",
        [
            ("1. HTTP Request Lifecycle & OWASP Security Defenses", [
                "Every incoming HTTP request in ASP.NET MVC traverses HttpApplication, RoutingEngine, ControllerFactory, ActionFilters, and ViewEngine.",
                "Cross-Site Request Forgery (CSRF) must be guarded against on all state-altering POST/PUT requests using [ValidateAntiForgeryToken] alongside @Html.AntiForgeryToken().",
                "Idempotency guarantees that issuing identical HTTP requests (GET, PUT, DELETE) produces identical server states, whereas POST submissions create fresh records and must be shielded with PRG (Post-Redirect-Get) patterns."
            ], "[HttpPost]\n[ValidateAntiForgeryToken]\npublic ActionResult SubmitAssignment(StudentSubmission model)\n{\n    if (!ModelState.IsValid) return View(model);\n    _repo.Save(model);\n    return RedirectToAction(\"Success\"); // PRG Pattern\n}"),

            ("2. Data Access Architecture: Repository & ADO.NET Parameters", [
                "Direct string concatenation into SQL queries exposes applications to critical SQL Injection vulnerabilities. Always employ parameterized SqlCommand parameters with explicit SqlDbType declarations.",
                "Transaction scopes ensure atomic ACID execution across multi-table insertions such as Order Header and Order Line items."
            ], "using (SqlCommand cmd = new SqlCommand(\"SELECT * FROM LMS.Users WHERE Email = @Email\", conn))\n{\n    cmd.Parameters.Add(\"@Email\", SqlDbType.NVarChar, 100).Value = email;\n    // Executes safely with parameter tokenization\n}"),

            ("3. Unit Testing & Dependency Inversion Principle (SOLID)", [
                "Controllers must never directly instantiate ADO.NET connections or concrete repositories. Inject repository interfaces via constructor injection to allow mock implementations during NUnit/xUnit testing.",
                "Action filter attributes encapsulate cross-cutting concerns including authentication, role gating, and structured exception logging."
            ], None)
        ]
    )

    # PDF 3: Python Data Science & ML Handbook
    create_study_guide_pdf(
        "Course4_Python_DataScience_ML_Handbook.pdf",
        "Data Engineering, Feature Pipelines & ML Deployment Handbook",
        "Course 4: Python for Data Science & Machine Learning",
        "Neha Kapoor (VP of Engineering & Distributed Systems)",
        [
            ("1. Vectorized Computing with NumPy & Pandas", [
                "Python loops introduce severe GIL and interpreter overhead when processing large numerical matrices. NumPy vectorization executes operations at compiled C/BLAS speeds.",
                "Avoid chained indexing (df['col'][idx]) in Pandas; use df.loc and df.iloc to guarantee view-versus-copy deterministic updates."
            ], "import numpy as np\nimport pandas as pd\n\n# Vectorized feature transformation\ndf['log_income'] = np.log1p(df['income'].clip(lower=0))\ndf['scaled_age'] = (df['age'] - df['age'].mean()) / df['age'].std()"),

            ("2. Cross-Validation & Preventing Data Leakage", [
                "Data leakage occurs when information from outside the training dataset is inadvertently used to create the model. Preprocessing transforms (imputation, scaling) must be fitted strictly on the train folds.",
                "Use StratifiedKFold for classification tasks where class balance is imbalanced to ensure equal target distribution across folds."
            ], "from sklearn.pipeline import Pipeline\nfrom sklearn.preprocessing import StandardScaler\nfrom sklearn.ensemble import RandomForestClassifier\n\npipeline = Pipeline([\n    ('scaler', StandardScaler()),\n    ('classifier', RandomForestClassifier(n_estimators=100, random_state=42))\n])"),

            ("3. Model Evaluation Metrics", [
                "Accuracy is misleading for imbalanced datasets (e.g. fraud detection with 99.9% negative cases). Instead, monitor Precision, Recall, F1-Score, and ROC-AUC curve distributions.",
                "Model explainability via SHAP (SHapley Additive exPlanations) values helps diagnose feature importance for regulatory compliance."
            ], None)
        ]
    )

    # PDF 4: Multi-Cloud AWS & Azure Cloud Architecture
    create_study_guide_pdf(
        "Course6_MultiCloud_AWS_Azure_Architecture.pdf",
        "Multi-Cloud High Availability & Disaster Recovery Guide",
        "Course 6: Multi-Cloud Architecture: AWS & Azure High Availability",
        "Dr. Vikram Sen (Principal Cloud Solutions Architect)",
        [
            ("1. Multi-Region Active-Active Routing & Global Load Balancing", [
                "Active-Active deployments require DNS latency-based routing (AWS Route 53 / Azure Traffic Manager) combined with health checks to automatically failover traffic within seconds.",
                "Stateless application tiers backed by auto-scaling container groups (AWS ECS / Azure Kubernetes Service) allow seamless geographic load distribution."
            ], "resource \"aws_route53_record\" \"global_app\" {\n  zone_id = var.dns_zone_id\n  name    = \"api.edupulse.com\"\n  type    = \"A\"\n  latency_routing_policy {\n    region = \"us-east-1\"\n  }\n  alias {\n    name                   = aws_lb.east_alb.dns_name\n    zone_id                = aws_lb.east_alb.zone_id\n    evaluate_target_health = true\n  }\n}"),

            ("2. Distributed Database Synchronization & CAP Theorem", [
                "Under network partitions (CAP theorem), systems must trade off immediate consistency for high availability. DynamoDB Global Tables and Azure Cosmos DB provide multi-region multi-master writes with tunable consistency levels (Session, Bounded Staleness, Eventual).",
                "Idempotency keys on REST APIs prevent double-charging or duplicate record writes during transient network retries across cloud boundaries."
            ], None),

            ("3. Zero-Trust Security & Multi-Cloud Identity Federation", [
                "Enforce mutual TLS (mTLS) across microservices and manage workload identities via OIDC federation (AWS IAM Roles for Service Accounts / Azure Workload Identity) rather than hardcoded long-lived credentials.",
                "All sensitive configuration strings must be encrypted in transit and at rest using cloud HSM key vaults (AWS KMS / Azure Key Vault) with strict audit access logging."
            ], None)
        ]
    )
