USE Practice;
GO

-- ============================================================================
-- 1. Create LMS.InstructorProfiles Table
-- ============================================================================
IF OBJECT_ID('LMS.InstructorProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.InstructorProfiles (
        ProfileId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL UNIQUE,
        PhoneNumber NVARCHAR(50) NULL,
        Headline NVARCHAR(200) NULL,
        City NVARCHAR(100) NULL,
        Country NVARCHAR(100) NULL,
        CurrentJobTitle NVARCHAR(150) NULL,
        CurrentOrganization NVARCHAR(150) NULL,
        TotalExperienceYears INT NULL DEFAULT 0,
        PastTeachingExperience NVARCHAR(MAX) NULL,
        PrimaryExpertise NVARCHAR(250) NULL,
        Bio NVARCHAR(MAX) NULL,
        HighestDegree NVARCHAR(100) NULL,
        InstitutionName NVARCHAR(200) NULL,
        GraduationYear INT NULL,
        PlannedCourses NVARCHAR(MAX) NULL,
        TargetLevel NVARCHAR(50) NULL DEFAULT 'Beginner',
        ProposedPricing DECIMAL(18,2) NULL DEFAULT 49.99,
        ProfilePictureUrl NVARCHAR(500) NULL,
        DigitalSignatureUrl NVARCHAR(500) NULL,
        LinkedInUrl NVARCHAR(250) NULL,
        GitHubOrPortfolioUrl NVARCHAR(250) NULL,
        CompletionPercentage INT NOT NULL DEFAULT 0,
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_InstructorProfiles_User FOREIGN KEY (UserId) REFERENCES LMS.Users(UserId) ON DELETE CASCADE
    );
END;
GO

-- ============================================================================
-- 2. Create LMS.CourseModules Table
-- ============================================================================
IF OBJECT_ID('LMS.CourseModules', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.CourseModules (
        ModuleId INT IDENTITY(1,1) PRIMARY KEY,
        CourseId INT NOT NULL,
        ModuleNumber INT NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        DurationMinutes INT NOT NULL DEFAULT 30,
        ContentMarkdown NVARCHAR(MAX) NULL,
        VideoOrResourceUrl NVARCHAR(500) NULL,
        AssignmentPrompt NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_CourseModules_Course FOREIGN KEY (CourseId) REFERENCES LMS.Courses(CourseId) ON DELETE CASCADE
    );
END;
GO

-- ============================================================================
-- 3. Create LMS.StudentSubmissions Table (Assignment / Quiz Review & Grading)
-- ============================================================================
IF OBJECT_ID('LMS.StudentSubmissions', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.StudentSubmissions (
        SubmissionId INT IDENTITY(1,1) PRIMARY KEY,
        CourseId INT NOT NULL,
        ModuleId INT NULL,
        UserId INT NOT NULL,
        SubmissionTitle NVARCHAR(200) NULL,
        SubmissionText NVARCHAR(MAX) NULL,
        SubmissionFileUrl NVARCHAR(500) NULL,
        SubmittedAt DATETIME NOT NULL DEFAULT GETDATE(),
        MarksObtained DECIMAL(5,2) NULL,
        MaxMarks DECIMAL(5,2) NOT NULL DEFAULT 100.00,
        InstructorFeedback NVARCHAR(MAX) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending Review', -- 'Pending Review', 'Approved', 'Rejected'
        ReviewedAt DATETIME NULL,
        ReviewedBy INT NULL,
        CONSTRAINT FK_StudentSubmissions_Course FOREIGN KEY (CourseId) REFERENCES LMS.Courses(CourseId) ON DELETE CASCADE,
        CONSTRAINT FK_StudentSubmissions_Module FOREIGN KEY (ModuleId) REFERENCES LMS.CourseModules(ModuleId) ON DELETE NO ACTION,
        CONSTRAINT FK_StudentSubmissions_User FOREIGN KEY (UserId) REFERENCES LMS.Users(UserId) ON DELETE NO ACTION,
        CONSTRAINT FK_StudentSubmissions_Reviewer FOREIGN KEY (ReviewedBy) REFERENCES LMS.Users(UserId) ON DELETE NO ACTION
    );
END;
GO

-- ============================================================================
-- 4. Stored Procedures for Instructor Profile Management
-- ============================================================================

-- 4.1 sp_GetInstructorProfile
CREATE OR ALTER PROCEDURE LMS.sp_GetInstructorProfile
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Ensure a profile row exists for this user
    IF NOT EXISTS (SELECT 1 FROM LMS.InstructorProfiles WHERE UserId = @UserId)
    BEGIN
        INSERT INTO LMS.InstructorProfiles (UserId, CompletionPercentage, UpdatedAt)
        VALUES (@UserId, 0, GETDATE());
    END;

    SELECT p.ProfileId, p.UserId, p.PhoneNumber, p.Headline, p.City, p.Country,
           p.CurrentJobTitle, p.CurrentOrganization, p.TotalExperienceYears,
           p.PastTeachingExperience, p.PrimaryExpertise, p.Bio,
           p.HighestDegree, p.InstitutionName, p.GraduationYear,
           p.PlannedCourses, p.TargetLevel, p.ProposedPricing,
           p.ProfilePictureUrl, p.DigitalSignatureUrl, p.LinkedInUrl, p.GitHubOrPortfolioUrl,
           p.CompletionPercentage, p.ReviewedAt, p.ReviewedBy, p.UpdatedAt,
           u.FullName, u.Email, u.Role, u.IsApproved, u.ApprovalStatus,
           COALESCE(NULLIF(p.AdminFeedback, ''), u.AdminFeedback, '') AS AdminFeedback,
           u.AdminFeedback AS UserAdminFeedback,
           u.CreatedAt AS RegisteredAt
    FROM LMS.InstructorProfiles p
    JOIN LMS.Users u ON p.UserId = u.UserId
    WHERE p.UserId = @UserId;
END;
GO

-- 4.2 sp_SaveInstructorProfile
CREATE OR ALTER PROCEDURE LMS.sp_SaveInstructorProfile
    @UserId INT,
    @PhoneNumber NVARCHAR(50),
    @Headline NVARCHAR(200),
    @City NVARCHAR(100),
    @Country NVARCHAR(100),
    @CurrentJobTitle NVARCHAR(150),
    @CurrentOrganization NVARCHAR(150),
    @TotalExperienceYears INT,
    @PastTeachingExperience NVARCHAR(MAX),
    @PrimaryExpertise NVARCHAR(250),
    @Bio NVARCHAR(MAX),
    @HighestDegree NVARCHAR(100),
    @InstitutionName NVARCHAR(200),
    @GraduationYear INT,
    @PlannedCourses NVARCHAR(MAX),
    @TargetLevel NVARCHAR(50),
    @ProposedPricing DECIMAL(18,2),
    @ProfilePictureUrl NVARCHAR(500),
    @DigitalSignatureUrl NVARCHAR(500),
    @LinkedInUrl NVARCHAR(250),
    @GitHubOrPortfolioUrl NVARCHAR(250)
AS
BEGIN
    SET NOCOUNT ON;

    -- Calculate Profile Completion Score (100-point formula):
    DECLARE @Score INT = 0;

    -- 1. Identity & Personal Details (20 pts)
    IF @PhoneNumber IS NOT NULL AND LEN(LTRIM(RTRIM(@PhoneNumber))) > 3 SET @Score = @Score + 5;
    IF @Headline IS NOT NULL AND LEN(LTRIM(RTRIM(@Headline))) > 3 SET @Score = @Score + 10;
    IF (@City IS NOT NULL AND LEN(LTRIM(RTRIM(@City))) > 1) OR (@Country IS NOT NULL AND LEN(LTRIM(RTRIM(@Country))) > 1) SET @Score = @Score + 5;

    -- 2. Professional & Teaching Experience (25 pts)
    IF @CurrentJobTitle IS NOT NULL AND LEN(LTRIM(RTRIM(@CurrentJobTitle))) > 2 SET @Score = @Score + 5;
    IF @TotalExperienceYears IS NOT NULL AND @TotalExperienceYears > 0 SET @Score = @Score + 5;
    IF @PastTeachingExperience IS NOT NULL AND LEN(LTRIM(RTRIM(@PastTeachingExperience))) > 10 SET @Score = @Score + 5;
    IF @PrimaryExpertise IS NOT NULL AND LEN(LTRIM(RTRIM(@PrimaryExpertise))) > 2 SET @Score = @Score + 5;
    IF @Bio IS NOT NULL AND LEN(LTRIM(RTRIM(@Bio))) >= 30 SET @Score = @Score + 5;

    -- 3. Education & Qualifications (20 pts)
    IF @HighestDegree IS NOT NULL AND LEN(LTRIM(RTRIM(@HighestDegree))) > 2 SET @Score = @Score + 10;
    IF @InstitutionName IS NOT NULL AND LEN(LTRIM(RTRIM(@InstitutionName))) > 2 SET @Score = @Score + 5;
    IF @GraduationYear IS NOT NULL AND @GraduationYear > 1950 SET @Score = @Score + 5;

    -- 4. Course Offerings & Pricing (15 pts)
    IF @PlannedCourses IS NOT NULL AND LEN(LTRIM(RTRIM(@PlannedCourses))) > 3 SET @Score = @Score + 5;
    IF @TargetLevel IS NOT NULL AND LEN(LTRIM(RTRIM(@TargetLevel))) > 1 SET @Score = @Score + 5;
    IF @ProposedPricing IS NOT NULL AND @ProposedPricing > 0 SET @Score = @Score + 5;

    -- 5. Digital Signature & Verification Assets (20 pts)
    IF @ProfilePictureUrl IS NOT NULL AND LEN(LTRIM(RTRIM(@ProfilePictureUrl))) > 5 SET @Score = @Score + 5;
    IF @DigitalSignatureUrl IS NOT NULL AND LEN(LTRIM(RTRIM(@DigitalSignatureUrl))) > 5 SET @Score = @Score + 10;
    IF (@LinkedInUrl IS NOT NULL AND LEN(LTRIM(RTRIM(@LinkedInUrl))) > 5) OR (@GitHubOrPortfolioUrl IS NOT NULL AND LEN(LTRIM(RTRIM(@GitHubOrPortfolioUrl))) > 5) SET @Score = @Score + 5;

    IF @Score > 100 SET @Score = 100;

    -- Upsert Profile
    IF NOT EXISTS (SELECT 1 FROM LMS.InstructorProfiles WHERE UserId = @UserId)
    BEGIN
        INSERT INTO LMS.InstructorProfiles (
            UserId, PhoneNumber, Headline, City, Country,
            CurrentJobTitle, CurrentOrganization, TotalExperienceYears, PastTeachingExperience, PrimaryExpertise, Bio,
            HighestDegree, InstitutionName, GraduationYear,
            PlannedCourses, TargetLevel, ProposedPricing,
            ProfilePictureUrl, DigitalSignatureUrl, LinkedInUrl, GitHubOrPortfolioUrl,
            CompletionPercentage, UpdatedAt
        )
        VALUES (
            @UserId, @PhoneNumber, @Headline, @City, @Country,
            @CurrentJobTitle, @CurrentOrganization, @TotalExperienceYears, @PastTeachingExperience, @PrimaryExpertise, @Bio,
            @HighestDegree, @InstitutionName, @GraduationYear,
            @PlannedCourses, @TargetLevel, @ProposedPricing,
            @ProfilePictureUrl, @DigitalSignatureUrl, @LinkedInUrl, @GitHubOrPortfolioUrl,
            @Score, GETDATE()
        );
    END
    ELSE
    BEGIN
        UPDATE LMS.InstructorProfiles
        SET PhoneNumber = @PhoneNumber,
            Headline = @Headline,
            City = @City,
            Country = @Country,
            CurrentJobTitle = @CurrentJobTitle,
            CurrentOrganization = @CurrentOrganization,
            TotalExperienceYears = @TotalExperienceYears,
            PastTeachingExperience = @PastTeachingExperience,
            PrimaryExpertise = @PrimaryExpertise,
            Bio = @Bio,
            HighestDegree = @HighestDegree,
            InstitutionName = @InstitutionName,
            GraduationYear = @GraduationYear,
            PlannedCourses = @PlannedCourses,
            TargetLevel = @TargetLevel,
            ProposedPricing = @ProposedPricing,
            ProfilePictureUrl = ISNULL(@ProfilePictureUrl, ProfilePictureUrl),
            DigitalSignatureUrl = ISNULL(@DigitalSignatureUrl, DigitalSignatureUrl),
            LinkedInUrl = @LinkedInUrl,
            GitHubOrPortfolioUrl = @GitHubOrPortfolioUrl,
            CompletionPercentage = @Score,
            UpdatedAt = GETDATE()
        WHERE UserId = @UserId;
    END;

    -- Return updated profile row
    EXEC LMS.sp_GetInstructorProfile @UserId = @UserId;
END;
GO

-- ============================================================================
-- 5. Stored Procedures for Dynamic Course Modules
-- ============================================================================

-- 5.1 sp_GetCourseModules
CREATE OR ALTER PROCEDURE LMS.sp_GetCourseModules
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ModuleId, CourseId, ModuleNumber, Title, Description, DurationMinutes, ContentMarkdown, VideoOrResourceUrl, AssignmentPrompt, CreatedAt
    FROM LMS.CourseModules
    WHERE CourseId = @CourseId
    ORDER BY ModuleNumber ASC, ModuleId ASC;
END;
GO

-- 5.2 sp_SaveCourseModule
CREATE OR ALTER PROCEDURE LMS.sp_SaveCourseModule
    @ModuleId INT = 0,
    @CourseId INT,
    @ModuleNumber INT,
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX),
    @DurationMinutes INT = 30,
    @ContentMarkdown NVARCHAR(MAX),
    @VideoOrResourceUrl NVARCHAR(500),
    @AssignmentPrompt NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    IF @ModuleId <= 0
    BEGIN
        -- Calculate next module number if not provided
        IF @ModuleNumber <= 0
        BEGIN
            SELECT @ModuleNumber = ISNULL(MAX(ModuleNumber), 0) + 1 FROM LMS.CourseModules WHERE CourseId = @CourseId;
        END;

        INSERT INTO LMS.CourseModules (CourseId, ModuleNumber, Title, Description, DurationMinutes, ContentMarkdown, VideoOrResourceUrl, AssignmentPrompt, CreatedAt)
        VALUES (@CourseId, @ModuleNumber, @Title, @Description, @DurationMinutes, @ContentMarkdown, @VideoOrResourceUrl, @AssignmentPrompt, GETDATE());

        SELECT SCOPE_IDENTITY() AS ModuleId;
    END
    ELSE
    BEGIN
        UPDATE LMS.CourseModules
        SET ModuleNumber = @ModuleNumber,
            Title = @Title,
            Description = @Description,
            DurationMinutes = @DurationMinutes,
            ContentMarkdown = @ContentMarkdown,
            VideoOrResourceUrl = @VideoOrResourceUrl,
            AssignmentPrompt = @AssignmentPrompt
        WHERE ModuleId = @ModuleId AND CourseId = @CourseId;

        SELECT @ModuleId AS ModuleId;
    END;
END;
GO

-- 5.3 sp_DeleteCourseModule
CREATE OR ALTER PROCEDURE LMS.sp_DeleteCourseModule
    @ModuleId INT,
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM LMS.CourseModules WHERE ModuleId = @ModuleId AND CourseId = @CourseId;
    SELECT 1 AS IsSuccess, 'Module deleted successfully.' AS Message;
END;
GO

-- ============================================================================
-- 6. Stored Procedures for Assignment / Quiz Review & Grading
-- ============================================================================

-- 6.1 sp_GetSubmissionsForInstructor
CREATE OR ALTER PROCEDURE LMS.sp_GetSubmissionsForInstructor
    @InstructorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.SubmissionId, s.CourseId, c.Title AS CourseTitle,
           s.ModuleId, m.Title AS ModuleTitle, m.ModuleNumber,
           s.UserId, u.FullName AS StudentName, u.Email AS StudentEmail,
           s.SubmissionTitle, s.SubmissionText, s.SubmissionFileUrl, s.SubmittedAt,
           s.MarksObtained, s.MaxMarks, s.InstructorFeedback, s.Status, s.ReviewedAt
    FROM LMS.StudentSubmissions s
    JOIN LMS.Courses c ON s.CourseId = c.CourseId
    JOIN LMS.Users u ON s.UserId = u.UserId
    LEFT JOIN LMS.CourseModules m ON s.ModuleId = m.ModuleId
    WHERE c.InstructorId = @InstructorId
    ORDER BY 
        CASE WHEN s.Status = 'Pending Review' THEN 0 ELSE 1 END,
        s.SubmittedAt DESC;
END;
GO

-- 6.2 sp_GradeSubmission
CREATE OR ALTER PROCEDURE LMS.sp_GradeSubmission
    @SubmissionId INT,
    @MarksObtained DECIMAL(5,2),
    @InstructorFeedback NVARCHAR(MAX),
    @Status NVARCHAR(50), -- 'Approved' or 'Rejected'
    @ReviewedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE LMS.StudentSubmissions
    SET MarksObtained = @MarksObtained,
        InstructorFeedback = @InstructorFeedback,
        Status = @Status,
        ReviewedAt = GETDATE(),
        ReviewedBy = @ReviewedBy
    WHERE SubmissionId = @SubmissionId;

    SELECT 1 AS IsSuccess, 'Submission successfully reviewed and graded.' AS Message;
END;
GO

-- 6.3 sp_GetStudentSubmissions
CREATE OR ALTER PROCEDURE LMS.sp_GetStudentSubmissions
    @UserId INT,
    @CourseId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.SubmissionId, s.CourseId, c.Title AS CourseTitle,
           s.ModuleId, m.Title AS ModuleTitle, m.ModuleNumber,
           s.UserId, s.SubmissionTitle, s.SubmissionText, s.SubmissionFileUrl, s.SubmittedAt,
           s.MarksObtained, s.MaxMarks, s.InstructorFeedback, s.Status, s.ReviewedAt,
           ISNULL(inst.FullName, c.InstructorName) AS InstructorName
    FROM LMS.StudentSubmissions s
    JOIN LMS.Courses c ON s.CourseId = c.CourseId
    LEFT JOIN LMS.CourseModules m ON s.ModuleId = m.ModuleId
    LEFT JOIN LMS.Users inst ON c.InstructorId = inst.UserId
    WHERE s.UserId = @UserId
      AND (@CourseId IS NULL OR s.CourseId = @CourseId)
    ORDER BY s.SubmittedAt DESC;
END;
GO

-- ============================================================================
-- 7. Seed Initial Instructor Profile & Modules for Course 1
-- ============================================================================
DECLARE @SharmaId INT;
SELECT TOP 1 @SharmaId = UserId FROM LMS.Users WHERE LOWER(Email) = 'asharma@lms.com';

IF @SharmaId IS NOT NULL
BEGIN
    EXEC LMS.sp_SaveInstructorProfile
        @UserId = @SharmaId,
        @PhoneNumber = '+91 98765 43210',
        @Headline = 'Principal Frontend Architect & Google Certified Developer',
        @City = 'Bengaluru',
        @Country = 'India',
        @CurrentJobTitle = 'Staff Engineer',
        @CurrentOrganization = 'TechSphere Labs',
        @TotalExperienceYears = 9,
        @PastTeachingExperience = 'Conducted 50+ corporate bootcamps, mentored 2,500+ developers in modern JavaScript, React 18, and scalable frontend architectures.',
        @PrimaryExpertise = 'React 18, TypeScript, Architecture, Next.js, Web Performance',
        @Bio = 'Passionate software craftsman and educator dedicated to helping students bridge the gap between basic coding tutorials and enterprise-grade software development.',
        @HighestDegree = 'Master of Technology in Computer Science',
        @InstitutionName = 'Indian Institute of Technology (IIT)',
        @GraduationYear = 2017,
        @PlannedCourses = 'Modern React 18, Microfrontends, Advanced TypeScript Patterns',
        @TargetLevel = 'Beginner to Intermediate',
        @ProposedPricing = 49.99,
        @ProfilePictureUrl = 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=300&q=80',
        @DigitalSignatureUrl = 'https://dummyimage.com/300x100/ffffff/000000.png&text=A.+Sharma',
        @LinkedInUrl = 'https://linkedin.com/in/asharma-instructor',
        @GitHubOrPortfolioUrl = 'https://github.com/asharma-dev';
END;

-- Seed Modules for Course 1 (Modern React 18 & TypeScript Masterclass)
DECLARE @Course1Id INT;
SELECT TOP 1 @Course1Id = CourseId FROM LMS.Courses WHERE Title LIKE '%React 18%';

IF @Course1Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LMS.CourseModules WHERE CourseId = @Course1Id)
BEGIN
    INSERT INTO LMS.CourseModules (CourseId, ModuleNumber, Title, Description, DurationMinutes, ContentMarkdown, VideoOrResourceUrl, AssignmentPrompt, CreatedAt)
    VALUES 
    (@Course1Id, 1, 'Module 1: Foundations of React 18 & Vite Ecosystem', 'Understand modern tooling, Vite setup, JSX compilation under the hood, and component tree fundamentals.', 35, '### Welcome to Modern React 18
In this module, you will initialize your project using Vite, master JSX semantics, and construct pure functional components with strict TypeScript types.', 'https://www.youtube.com/embed/bMknfKXIFA8', 'Create a reusable Button and Card component with TypeScript strict prop types.', GETDATE()),
    (@Course1Id, 2, 'Module 2: State Primitives & Advanced Hooks', 'Master useState, useReducer, useEffect lifecycles, and build custom hooks for network status and local storage.', 45, '### State Management Mastery
Explore the reactive component model. Learn when to isolate state locally versus hoisting state to parent contexts.', 'https://www.youtube.com/embed/O6P86uwfdR0', 'Build a custom hook `useDebounce` and demonstrate its use in a real-time search input.', GETDATE()),
    (@Course1Id, 3, 'Module 3: Asynchronous REST & Server State', 'Handle API calls, loading indicators, graceful error boundaries, and optimistic UI updates.', 50, '### Async Data Patterns
Understand HTTP side-effects, cancellation tokens using AbortController, and caching data.', 'https://www.youtube.com/embed/9boMnM5X93o', 'Implement a paginated GitHub repository explorer fetching data from the GitHub REST API.', GETDATE()),
    (@Course1Id, 4, 'Module 4: Performance Profiling & Production Deployment', 'Analyze React DevTools profiler traces, memoization with useMemo/useCallback, and deploy to Vercel/Netlify.', 40, '### Shipping Production React
Learn how to eliminate unnecessary re-renders, implement route-based code splitting with React.lazy, and bundle optimizations.', 'https://www.youtube.com/embed/DPX_kLg5u-0', 'Submit your deployed Vercel live application link with bundle analysis screenshots.', GETDATE());
END;
GO

PRINT 'Instructor Architecture DB migration successfully executed!';
