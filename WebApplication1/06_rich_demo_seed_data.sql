-- ============================================================================
-- 06_rich_demo_seed_data.sql
-- Seeds high-fidelity real-world users, complete instructor profiles,
-- courses, curriculum modules, practical assignments, quizzes, enrollments,
-- and verified assignment submissions with instructor feedback.
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

-- 1. SEED INSTRUCTORS & STUDENTS
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE Email = 'vsen@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('Dr. Vikram Sen', 'vsen@lms.com', 'Pass@123', 'Instructor', 1, 'Approved', DATEADD(DAY, -60, GETDATE()));
END;

IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE Email = 'nkapoor@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('Neha Kapoor', 'nkapoor@lms.com', 'Pass@123', 'Instructor', 1, 'Approved', DATEADD(DAY, -45, GETDATE()));
END;

IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE Email = 'rahul.verma@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('Rahul Verma', 'rahul.verma@lms.com', 'Pass@123', 'Student', 1, 'Approved', DATEADD(DAY, -30, GETDATE()));
END;

IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE Email = 'priya.sharma@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('Priya Sharma', 'priya.sharma@lms.com', 'Pass@123', 'Student', 1, 'Approved', DATEADD(DAY, -25, GETDATE()));
END;

IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE Email = 'marcus.vance@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('Marcus Vance', 'marcus.vance@lms.com', 'Pass@123', 'Student', 1, 'Approved', DATEADD(DAY, -15, GETDATE()));
END;

-- Clean SVG Data URIs for Instructor Digital Signatures
DECLARE @SigASharma NVARCHAR(MAX) = 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="260" height="70" viewBox="0 0 260 70"><path d="M20 45 C45 10, 60 55, 80 25 C100 5, 120 40, 150 20 C180 5, 210 35, 240 25 M30 50 L180 50" fill="none" stroke="%231e3a8a" stroke-width="2.5" stroke-linecap="round"/></svg>';
DECLARE @SigVSen NVARCHAR(MAX) = 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="260" height="70" viewBox="0 0 260 70"><path d="M15 25 Q35 60 65 20 T115 45 T165 15 T215 40 T245 25 M40 55 Q130 50 220 52" fill="none" stroke="%230f766e" stroke-width="2.8" stroke-linecap="round"/></svg>';
DECLARE @SigNKapoor NVARCHAR(MAX) = 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="260" height="70" viewBox="0 0 260 70"><path d="M25 40 C40 15, 65 55, 95 30 S145 10 175 40 S215 20 245 35 M50 52 L200 48" fill="none" stroke="%237c3aed" stroke-width="2.6" stroke-linecap="round"/></svg>';

-- 2. COMPLETE INSTRUCTOR PROFILES (>= 90% Completion)
-- 2.1 A. Sharma (UserId = 1)
DECLARE @IdASharma INT = (SELECT UserId FROM LMS.Users WHERE Email = 'asharma@lms.com');
IF @IdASharma IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM LMS.InstructorProfiles WHERE UserId = @IdASharma)
        INSERT INTO LMS.InstructorProfiles (UserId, CompletionPercentage, UpdatedAt) VALUES (@IdASharma, 0, GETDATE());

    UPDATE LMS.InstructorProfiles
    SET PhoneNumber = '+91 98765 43210',
        Headline = 'Principal Frontend Architect & Google Certified Web Expert',
        City = 'Bengaluru',
        Country = 'India',
        CurrentJobTitle = 'Staff Frontend Engineer',
        CurrentOrganization = 'TechSphere Global',
        TotalExperienceYears = 12,
        PastTeachingExperience = 'Taught modern JavaScript and reactive UI systems to 8,000+ corporate engineers across Fortune 500 bootcamps and university tech symposiums.',
        PrimaryExpertise = 'React 18, TypeScript Architecture, Web Performance Optimization, Next.js Ecosystem',
        Bio = 'Passionate educator and systems architect with over a decade of production experience building high-scale distributed web applications. Former Lead Architect at FinTech scaleups.',
        HighestDegree = 'Master of Technology (M.Tech)',
        InstitutionName = 'Indian Institute of Technology (IIT) Delhi',
        GraduationYear = 2014,
        PlannedCourses = 'Modern React 18, Advanced TypeScript Patterns, Enterprise Web Performance',
        TargetLevel = 'Intermediate to Advanced',
        ProposedPricing = 79.99,
        ProfilePictureUrl = 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&q=80',
        DigitalSignatureUrl = @SigASharma,
        LinkedInUrl = 'https://linkedin.com/in/asharma-architect',
        GitHubOrPortfolioUrl = 'https://github.com/asharma-frontend',
        CompletionPercentage = 100,
        ReviewedAt = DATEADD(DAY, -50, GETDATE()),
        ReviewedBy = 3,
        UpdatedAt = GETDATE()
    WHERE UserId = @IdASharma;
END;

-- 2.2 Dr. Vikram Sen (Cloud & DevOps)
DECLARE @IdVSen INT = (SELECT UserId FROM LMS.Users WHERE Email = 'vsen@lms.com');
IF @IdVSen IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM LMS.InstructorProfiles WHERE UserId = @IdVSen)
        INSERT INTO LMS.InstructorProfiles (UserId, CompletionPercentage, UpdatedAt) VALUES (@IdVSen, 0, GETDATE());

    UPDATE LMS.InstructorProfiles
    SET PhoneNumber = '+1 415 555 0192',
        Headline = 'Senior Cloud Solutions Architect & Ex-AWS Principal Lead',
        City = 'Seattle',
        Country = 'United States',
        CurrentJobTitle = 'Principal Cloud Architect',
        CurrentOrganization = 'CloudScale Dynamics',
        TotalExperienceYears = 14,
        PastTeachingExperience = 'Adjunct Professor of Distributed Systems; trained AWS certification candidates with 98% pass rate across 5 consecutive years.',
        PrimaryExpertise = 'Multi-Cloud Architecture (AWS/GCP), Kubernetes, Infrastructure as Code, High Availability',
        Bio = 'Published author and enterprise architect specializing in multi-region failover, cloud financial engineering (FinOps), and zero-trust perimeter security.',
        HighestDegree = 'Doctor of Philosophy (PhD) in Computer Science',
        InstitutionName = 'Stanford University',
        GraduationYear = 2012,
        PlannedCourses = 'Multi-Cloud Architecture on AWS & GCP, Docker & Kubernetes Production Mastery',
        TargetLevel = 'Advanced',
        ProposedPricing = 89.99,
        ProfilePictureUrl = 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=400&q=80',
        DigitalSignatureUrl = @SigVSen,
        LinkedInUrl = 'https://linkedin.com/in/dr-vikram-sen',
        GitHubOrPortfolioUrl = 'https://github.com/vsen-cloud',
        CompletionPercentage = 96,
        ReviewedAt = DATEADD(DAY, -40, GETDATE()),
        ReviewedBy = 3,
        UpdatedAt = GETDATE()
    WHERE UserId = @IdVSen;
END;

-- 2.3 Neha Kapoor (AI & Machine Learning)
DECLARE @IdNKapoor INT = (SELECT UserId FROM LMS.Users WHERE Email = 'nkapoor@lms.com');
IF @IdNKapoor IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM LMS.InstructorProfiles WHERE UserId = @IdNKapoor)
        INSERT INTO LMS.InstructorProfiles (UserId, CompletionPercentage, UpdatedAt) VALUES (@IdNKapoor, 0, GETDATE());

    UPDATE LMS.InstructorProfiles
    SET PhoneNumber = '+91 98111 22334',
        Headline = 'Principal AI/ML Engineer & Generative AI Solutions Lead',
        City = 'Hyderabad',
        Country = 'India',
        CurrentJobTitle = 'Staff Machine Learning Engineer',
        CurrentOrganization = 'NeuralCore Labs',
        TotalExperienceYears = 9,
        PastTeachingExperience = 'Speaker at PyData and NeurIPS workshops; mentored 4,500+ aspiring data scientists in applied deep learning and NLP architectures.',
        PrimaryExpertise = 'Python AI/ML, PyTorch, Retrieval-Augmented Generation (RAG), LLM Fine-Tuning, MLOps',
        Bio = 'Dedicated researcher bringing complex mathematical models into production scale. Specializes in transformer architectures, semantic embeddings, and agentic workflows.',
        HighestDegree = 'Master of Science (M.S.) in Data Science',
        InstitutionName = 'BITS Pilani',
        GraduationYear = 2017,
        PlannedCourses = 'Applied Machine Learning & Generative AI with Python, Production LLM Engineering',
        TargetLevel = 'Intermediate',
        ProposedPricing = 69.99,
        ProfilePictureUrl = 'https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?auto=format&fit=crop&w=400&q=80',
        DigitalSignatureUrl = @SigNKapoor,
        LinkedInUrl = 'https://linkedin.com/in/neha-kapoor-ml',
        GitHubOrPortfolioUrl = 'https://github.com/nkapoor-ai',
        CompletionPercentage = 94,
        ReviewedAt = DATEADD(DAY, -35, GETDATE()),
        ReviewedBy = 3,
        UpdatedAt = GETDATE()
    WHERE UserId = @IdNKapoor;
END;

-- 3. LINK INSTRUCTORS TO COURSES
IF @IdVSen IS NOT NULL
BEGIN
    UPDATE LMS.Courses SET InstructorId = @IdVSen WHERE CourseId IN (4, 6);
END;
IF @IdNKapoor IS NOT NULL
BEGIN
    UPDATE LMS.Courses SET InstructorId = @IdNKapoor WHERE CourseId = 5;
END;

-- 4. SEED DETAILED MODULES FOR COURSES 2, 4, 6
-- 4.1 Course 2: Enterprise ASP.NET MVC 5 & SQL Server Architecture
IF NOT EXISTS (SELECT 1 FROM LMS.CourseModules WHERE CourseId = 2)
BEGIN
    INSERT INTO LMS.CourseModules (CourseId, ModuleNumber, Title, Description, DurationMinutes, ContentMarkdown, VideoOrResourceUrl, AssignmentPrompt)
    VALUES 
    (2, 1, 'Module 1: MVC Architectural Lifecycle & HTTP Pipeline', 
     'Deep dive into the ASP.NET MVC request processing lifecycle, routing tables, and HTTP handlers.', 
     45, 
     '### ASP.NET MVC Request Lifecycle' + CHAR(13) + CHAR(10) +
     'Understanding how an HTTP request travels through IIS, UrlRoutingModule, ControllerFactory, ActionInvoker, and ViewEngine.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     '#### Key Lifecycle Stages:' + CHAR(13) + CHAR(10) +
     '1. **Routing:** Incoming URL parsed by RouteTable against configured route templates.' + CHAR(13) + CHAR(10) +
     '2. **Controller Activation:** DefaultControllerFactory instantiates controller using Dependency Injection.' + CHAR(13) + CHAR(10) +
     '3. **Action Execution:** Action filters execute (Authorization, Action, Result, Exception).' + CHAR(13) + CHAR(10) +
     '4. **Result Execution:** ActionResult renders View via Razor ViewEngine.',
     'https://www.youtube.com/embed/dQw4w9WgXcQ',
     'Create a custom ActionFilterAttribute named `ExecutionTimingAttribute` that logs controller action execution duration in milliseconds to the Output debug stream.'),

    (2, 2, 'Module 2: Repository Pattern & SQL Server Stored Procedures', 
     'Decoupling data access logic using interfaces, ADO.NET DbHelper, and parameterized stored procedures.', 
     50, 
     '### Scalable Data Access with Repository Pattern' + CHAR(13) + CHAR(10) +
     'Direct SQL in controllers causes high coupling and security vulnerabilities. By abstracting data operations behind interfaces, your application gains testability and query optimization control.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     '#### Stored Procedure Best Practices:' + CHAR(13) + CHAR(10) +
     '- Always specify `SET NOCOUNT ON;` to eliminate network round-trips for row count.' + CHAR(13) + CHAR(10) +
     '- Use strictly typed `SqlParameter` objects to prevent SQL Injection.' + CHAR(13) + CHAR(10) +
     '- Use transactions (`BEGIN TRANSACTION ... COMMIT`) for multi-table updates.',
     'https://www.youtube.com/embed/dQw4w9WgXcQ',
     'Write a complete SQL Server stored procedure `sp_GetStudentReport` with pagination (@PageNumber, @PageSize) and implement its repository caller method in C#.'),

    (2, 3, 'Module 3: Enterprise Security, CSRF Tokens & RBAC Filters', 
     'Hardening web applications against CSRF, XSS, and unauthorized role elevation.', 
     60, 
     '### Enterprise ASP.NET Security' + CHAR(13) + CHAR(10) +
     'Securing production enterprise systems requires defense in depth across cookies, session tokens, and input sanitization.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     '#### Security Checklist:' + CHAR(13) + CHAR(10) +
     '- Anti-Forgery Tokens: `@Html.AntiForgeryToken()` + `[ValidateAntiForgeryToken]` on all state-altering POST endpoints.' + CHAR(13) + CHAR(10) +
     '- Role Authorization: Custom `[AuthorizeRoles("Admin", "Instructor")]` filter attribute.' + CHAR(13) + CHAR(10) +
     '- Secure Cookies: `HttpOnly = true`, `Secure = true`, `SameSite = SameSiteMode.Lax`.',
     'https://www.youtube.com/embed/dQw4w9WgXcQ',
     'Implement a custom `RoleAuthorizeAttribute` that intercepts unapproved instructor accounts and redirects them to a specialized onboarding review page.'),

    (2, 4, 'Module 4: Performance Profiling, Bundling & Index Tuning', 
     'Optimizing throughput with database nonclustered indexes and minified asset pipelines.', 
     40, 
     '### Performance Tuning & Database Indexing' + CHAR(13) + CHAR(10) +
     'High-traffic applications require low database latency and small payload sizes.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     '#### Key Optimizations:' + CHAR(13) + CHAR(10) +
     '- Filtered nonclustered indexes for active records (`WHERE IsDeleted = 0`).' + CHAR(13) + CHAR(10) +
     '- BundleConfig script combining and minification via WebGrease.' + CHAR(13) + CHAR(10) +
     '- Proper static MIME type mappings in Web.config for webp and font files.',
     'https://www.youtube.com/embed/dQw4w9WgXcQ',
     'Analyze an execution plan in SQL Server Management Studio and write a filtered index migration script for course enrollments.');
END;

-- 4.2 Course 6: Multi-Cloud Architecture on AWS and GCP
IF NOT EXISTS (SELECT 1 FROM LMS.CourseModules WHERE CourseId = 6)
BEGIN
    INSERT INTO LMS.CourseModules (CourseId, ModuleNumber, Title, Description, DurationMinutes, ContentMarkdown, VideoOrResourceUrl, AssignmentPrompt)
    VALUES 
    (6, 1, 'Module 1: Multi-Cloud Networking, VPC Peering & Transit Gateways', 
     'Designing interconnected network topologies bridging AWS VPCs and GCP Virtual Private Clouds.', 
     55, 
     '### Multi-Cloud Interconnect Architecture' + CHAR(13) + CHAR(10) +
     'Modern enterprises prevent single-cloud vendor lock-in by designing resilient, low-latency hybrid cloud network perimeters.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     '#### Architecture Principles:' + CHAR(13) + CHAR(10) +
     '- Non-overlapping CIDR blocks across AWS (10.0.0.0/16) and GCP (10.1.0.0/16).' + CHAR(13) + CHAR(10) +
     '- Redundant IPsec VPN tunnels with BGP dynamic routing.' + CHAR(13) + CHAR(10) +
     '- Transit Gateways for centralized hub-and-spoke routing.',
     'https://www.youtube.com/embed/dQw4w9WgXcQ',
     'Draw or document a complete CIDR allocation plan and routing table design for an e-commerce platform spanning AWS us-east-1 and GCP us-central1.'),

    (6, 2, 'Module 2: Container Orchestration Across EKS and GKE', 
     'Deploying unified microservices workloads using Kubernetes, Helm, and GitOps pipelines.', 
     65, 
     '### Unified Kubernetes Management' + CHAR(13) + CHAR(10) +
     'Standardizing deployments across Amazon Elastic Kubernetes Service (EKS) and Google Kubernetes Engine (GKE).' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     '#### Key Stack Components:' + CHAR(13) + CHAR(10) +
     '- Terraform for multi-cloud cluster provisioning.' + CHAR(13) + CHAR(10) +
     '- ArgoCD GitOps operator synchronizing manifests to both clusters.' + CHAR(13) + CHAR(10) +
     '- ExternalDNS and Route53 weighted latency-based routing.',
     'https://www.youtube.com/embed/dQw4w9WgXcQ',
     'Write a Kubernetes Deployment manifest and HorizontalPodAutoscaler YAML configured for high availability across multi-zone nodes.'),

    (6, 3, 'Module 3: Multi-Cloud Database Replication & Disaster Recovery', 
     'Active-active and active-passive database topologies across Amazon Aurora and Cloud Spanner.', 
     50, 
     '### Cross-Cloud Data Synchronization' + CHAR(13) + CHAR(10) +
     'Data persistence is the hardest challenge in multi-cloud engineering. Learn conflict-free replicated data types and asynchronous CDC.' + CHAR(13) + CHAR(10) + CHAR(13) + CHAR(10) +
     '#### Topics Covered:' + CHAR(13) + CHAR(10) +
     '- Change Data Capture (CDC) with Debezium and Apache Kafka.' + CHAR(13) + CHAR(10) +
     '- RPO (Recovery Point Objective) and RTO (Recovery Time Objective) calculations.' + CHAR(13) + CHAR(10) +
     '- Automated failover health checks with Cloudflare Global Traffic Manager.',
     'https://www.youtube.com/embed/dQw4w9WgXcQ',
     'Calculate RPO and RTO for a simulated primary AWS outage and specify the automated DNS switchover steps to GCP.');
END;

-- 5. SEED QUIZZES FOR COURSES 2 & 6
IF NOT EXISTS (SELECT 1 FROM LMS.QuizQuestions WHERE CourseId = 2)
BEGIN
    INSERT INTO LMS.QuizQuestions (CourseId, LessonNumber, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectOption)
    VALUES
    (2, 1, 'Which component in the ASP.NET MVC request lifecycle is responsible for locating and instantiating the Controller class?', 'UrlRoutingModule', 'DefaultControllerFactory', 'ActionInvoker', 'RazorViewEngine', 'B'),
    (2, 2, 'Why is `SET NOCOUNT ON;` recommended at the beginning of SQL Server stored procedures?', 'It stops SQL Server from executing queries', 'It eliminates network messages reporting rows affected, improving performance', 'It enables transactions automatically', 'It disables parameter checking', 'B'),
    (2, 3, 'Which attribute should be placed on state-modifying POST actions to prevent Cross-Site Request Forgery (CSRF)?', '[AllowAnonymous]', '[AuthorizeRoles]', '[ValidateAntiForgeryToken]', '[OutputCache]', 'C'),
    (2, 4, 'What is the primary benefit of a filtered nonclustered index in SQL Server?', 'It indexes only rows meeting a specific WHERE predicate, saving storage and improving seek speed', 'It sorts all columns in descending order', 'It locks the entire table during writes', 'It replaces the primary key', 'A');
END;

IF NOT EXISTS (SELECT 1 FROM LMS.QuizQuestions WHERE CourseId = 6)
BEGIN
    INSERT INTO LMS.QuizQuestions (CourseId, LessonNumber, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectOption)
    VALUES
    (6, 1, 'What is the most critical networking prerequisite before peering an AWS VPC with a GCP Virtual Private Cloud?', 'Both clouds must use identical CIDR blocks', 'CIDR IP address ranges must be strictly non-overlapping', 'BGP routing must be turned off', 'Both must be in the same physical datacenter', 'B'),
    (6, 2, 'Which GitOps tool can be used to synchronize Kubernetes manifests consistently across both AWS EKS and GCP GKE clusters?', 'Docker Swarm', 'ArgoCD', 'Jenkins Master', 'Ansible Tower', 'B'),
    (6, 3, 'In disaster recovery architecture, what does RPO (Recovery Point Objective) measure?', 'The maximum acceptable duration of data loss measured in time prior to the disaster', 'The time required to restore compute servers', 'The total dollar cost of cloud resources', 'The latency of network packets', 'A');
END;

-- 6. SEED ENROLLMENTS FOR STUDENTS
DECLARE @IdStudent INT = (SELECT UserId FROM LMS.Users WHERE Email = 'student@lms.com');
DECLARE @IdRahul INT = (SELECT UserId FROM LMS.Users WHERE Email = 'rahul.verma@lms.com');
DECLARE @IdPriya INT = (SELECT UserId FROM LMS.Users WHERE Email = 'priya.sharma@lms.com');

IF @IdStudent IS NOT NULL
BEGIN
    -- Course 1: Completed 100%
    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @IdStudent AND CourseId = 1)
        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
        VALUES (@IdStudent, 1, DATEADD(DAY, -20, GETDATE()), 100, 1);
    ELSE
        UPDATE LMS.Enrollments SET ProgressPercentage = 100, IsCompleted = 1 WHERE UserId = @IdStudent AND CourseId = 1;

    -- Course 2: In Progress 75%
    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @IdStudent AND CourseId = 2)
        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
        VALUES (@IdStudent, 2, DATEADD(DAY, -10, GETDATE()), 75, 0);

    -- Course 6: In Progress 33%
    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @IdStudent AND CourseId = 6)
        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
        VALUES (@IdStudent, 6, DATEADD(DAY, -5, GETDATE()), 33, 0);
END;

IF @IdRahul IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @IdRahul AND CourseId = 1)
        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
        VALUES (@IdRahul, 1, DATEADD(DAY, -12, GETDATE()), 50, 0);

    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @IdRahul AND CourseId = 6)
        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
        VALUES (@IdRahul, 6, DATEADD(DAY, -7, GETDATE()), 66, 0);
END;

IF @IdPriya IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @IdPriya AND CourseId = 2)
        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
        VALUES (@IdPriya, 2, DATEADD(DAY, -18, GETDATE()), 100, 1);
END;

-- 7. SEED VERIFIED ASSIGNMENT SUBMISSIONS & MARKS (FOR STUDENT @ LMS.COM)
IF @IdStudent IS NOT NULL
BEGIN
    -- Submissions for Course 1
    DECLARE @Mod1 INT = (SELECT TOP 1 ModuleId FROM LMS.CourseModules WHERE CourseId = 1 AND ModuleNumber = 1);
    DECLARE @Mod2 INT = (SELECT TOP 1 ModuleId FROM LMS.CourseModules WHERE CourseId = 1 AND ModuleNumber = 2);
    DECLARE @Mod3 INT = (SELECT TOP 1 ModuleId FROM LMS.CourseModules WHERE CourseId = 1 AND ModuleNumber = 3);
    DECLARE @Mod4 INT = (SELECT TOP 1 ModuleId FROM LMS.CourseModules WHERE CourseId = 1 AND ModuleNumber = 4);

    -- Module 1: Approved 95/100
    IF @Mod1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LMS.StudentSubmissions WHERE UserId = @IdStudent AND CourseId = 1 AND ModuleId = @Mod1)
    BEGIN
        INSERT INTO LMS.StudentSubmissions (CourseId, ModuleId, UserId, SubmissionTitle, SubmissionText, SubmissionFileUrl, SubmittedAt, MarksObtained, MaxMarks, InstructorFeedback, Status, ReviewedAt, ReviewedBy)
        VALUES (1, @Mod1, @IdStudent, 'React 18 Vite Scaffold & Strict Prop Interfaces', 
                'Implemented reusable Button and Card components in TypeScript with strict Union prop types for size and variant. Configured Vitest for unit testing.',
                'https://github.com/student-demo/react18-vite-starter', DATEADD(DAY, -18, GETDATE()), 
                95, 100, 'Superb component separation and prop typing! Great use of discriminated unions for the button variant.', 'Approved', DATEADD(DAY, -17, GETDATE()), 1);
    END;

    -- Module 2: Approved 92/100
    IF @Mod2 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LMS.StudentSubmissions WHERE UserId = @IdStudent AND CourseId = 1 AND ModuleId = @Mod2)
    BEGIN
        INSERT INTO LMS.StudentSubmissions (CourseId, ModuleId, UserId, SubmissionTitle, SubmissionText, SubmissionFileUrl, SubmittedAt, MarksObtained, MaxMarks, InstructorFeedback, Status, ReviewedAt, ReviewedBy)
        VALUES (1, @Mod2, @IdStudent, 'Custom Hook State Management (`useLocalStorage`)', 
                'Built custom useLocalStorage hook synchronizing state with browser window storage events and handling serialization errors.',
                'https://github.com/student-demo/react18-custom-hooks', DATEADD(DAY, -14, GETDATE()), 
                92, 100, 'Solid event handling and edge-case error capture. Consider memoizing the return tuple.', 'Approved', DATEADD(DAY, -13, GETDATE()), 1);
    END;

    -- Module 3: Approved 88/100
    IF @Mod3 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LMS.StudentSubmissions WHERE UserId = @IdStudent AND CourseId = 1 AND ModuleId = @Mod3)
    BEGIN
        INSERT INTO LMS.StudentSubmissions (CourseId, ModuleId, UserId, SubmissionTitle, SubmissionText, SubmissionFileUrl, SubmittedAt, MarksObtained, MaxMarks, InstructorFeedback, Status, ReviewedAt, ReviewedBy)
        VALUES (1, @Mod3, @IdStudent, 'Async REST Client with AbortController', 
                'Constructed resilient data-fetching hook with automatic request cancellation upon component unmount and retry backoff.',
                'https://github.com/student-demo/react18-rest-abort', DATEADD(DAY, -8, GETDATE()), 
                88, 100, 'Well-structured AbortController integration. Meets all course verification benchmarks.', 'Approved', DATEADD(DAY, -7, GETDATE()), 1);
    END;

    -- Module 4: Approved 96/100
    IF @Mod4 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LMS.StudentSubmissions WHERE UserId = @IdStudent AND CourseId = 1 AND ModuleId = @Mod4)
    BEGIN
        INSERT INTO LMS.StudentSubmissions (CourseId, ModuleId, UserId, SubmissionTitle, SubmissionText, SubmissionFileUrl, SubmittedAt, MarksObtained, MaxMarks, InstructorFeedback, Status, ReviewedAt, ReviewedBy)
        VALUES (1, @Mod4, @IdStudent, 'Production Build & React Profiler Optimization', 
                'Profiled rendering bottlenecks with Chrome DevTools and React Profiler. Reduced re-render count by 64% using React.memo and useCallback.',
                'https://github.com/student-demo/react18-profiler-perf', DATEADD(DAY, -3, GETDATE()), 
                96, 100, 'Exemplary performance audit! Course completion certificate unlocked.', 'Approved', DATEADD(DAY, -2, GETDATE()), 1);
    END;

    -- Submissions for Course 2 (ASP.NET MVC) - Module 1: Pending Review
    DECLARE @C2Mod1 INT = (SELECT TOP 1 ModuleId FROM LMS.CourseModules WHERE CourseId = 2 AND ModuleNumber = 1);
    IF @C2Mod1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LMS.StudentSubmissions WHERE UserId = @IdStudent AND CourseId = 2 AND ModuleId = @C2Mod1)
    BEGIN
        INSERT INTO LMS.StudentSubmissions (CourseId, ModuleId, UserId, SubmissionTitle, SubmissionText, SubmissionFileUrl, SubmittedAt, MarksObtained, MaxMarks, InstructorFeedback, Status)
        VALUES (2, @C2Mod1, @IdStudent, 'Custom ActionFilterAttribute for Execution Timing', 
                'Created StopwatchActionFilter logging controller action duration in milliseconds to System.Diagnostics.Debug.',
                'https://github.com/student-demo/aspnet-timing-filter', DATEADD(DAY, -1, GETDATE()), 
                NULL, 100, NULL, 'Pending');
    END;
END;

-- 8. SEED QUIZ ATTEMPTS
IF @IdStudent IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM LMS.StudentQuizAttempts WHERE UserId = @IdStudent AND CourseId = 1 AND LessonNumber = 1)
        INSERT INTO LMS.StudentQuizAttempts (UserId, CourseId, LessonNumber, TotalQuestions, CorrectAnswers, ScorePercentage, IsPassed, AttemptedAt)
        VALUES (@IdStudent, 1, 1, 4, 4, 100.0, 1, DATEADD(DAY, -18, GETDATE()));

    IF NOT EXISTS (SELECT 1 FROM LMS.StudentQuizAttempts WHERE UserId = @IdStudent AND CourseId = 1 AND LessonNumber = 2)
        INSERT INTO LMS.StudentQuizAttempts (UserId, CourseId, LessonNumber, TotalQuestions, CorrectAnswers, ScorePercentage, IsPassed, AttemptedAt)
        VALUES (@IdStudent, 1, 2, 4, 4, 100.0, 1, DATEADD(DAY, -14, GETDATE()));

    IF NOT EXISTS (SELECT 1 FROM LMS.StudentQuizAttempts WHERE UserId = @IdStudent AND CourseId = 2 AND LessonNumber = 1)
        INSERT INTO LMS.StudentQuizAttempts (UserId, CourseId, LessonNumber, TotalQuestions, CorrectAnswers, ScorePercentage, IsPassed, AttemptedAt)
        VALUES (@IdStudent, 2, 1, 4, 3, 75.0, 1, DATEADD(DAY, -1, GETDATE()));
END;

PRINT '======================================================================';
PRINT ' 06_rich_demo_seed_data.sql EXECUTED SUCCESSFULLY!';
PRINT '======================================================================';
