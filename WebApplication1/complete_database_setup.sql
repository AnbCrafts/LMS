-- ============================================================================
-- EduPulse LMS - Complete Database Setup Script
-- Target Database: Practice
-- Description: Sets up Schema, all Tables, Constraints, Stored Procedures,
--              and Seed Data (Admin, Instructors, Students, Courses, Reviews, Quizzes).
-- ============================================================================

-- 1. Ensure Database Exists
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'Practice')
BEGIN
    CREATE DATABASE Practice;
END;
GO

USE Practice;
GO

-- 2. Ensure Schema Exists
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'LMS')
BEGIN
    EXEC('CREATE SCHEMA LMS');
END;
GO

-- ============================================================================
-- 3. TABLES CREATION
-- ============================================================================

-- 3.1 Users Table
IF OBJECT_ID('LMS.Users', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Users (
        UserId INT IDENTITY(1,1) PRIMARY KEY,
        FullName NVARCHAR(150) NOT NULL,
        Email NVARCHAR(150) NOT NULL UNIQUE,
        Password NVARCHAR(255) NOT NULL,
        Role NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'Student',
        IsApproved BIT NOT NULL CONSTRAINT DF_Users_IsApproved DEFAULT 1,
        ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_ApprovalStatus DEFAULT 'Approved',
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT GETDATE()
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'Role')
        ALTER TABLE LMS.Users ADD Role NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'Student';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'IsApproved')
        ALTER TABLE LMS.Users ADD IsApproved BIT NOT NULL CONSTRAINT DF_Users_IsApproved DEFAULT 1;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'ApprovalStatus')
        ALTER TABLE LMS.Users ADD ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_ApprovalStatus DEFAULT 'Approved';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'CreatedAt')
        ALTER TABLE LMS.Users ADD CreatedAt DATETIME NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT GETDATE();
END;
GO

-- 3.2 Courses Table
IF OBJECT_ID('LMS.Courses', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Courses (
        CourseId INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        InstructorId INT NULL,
        InstructorName NVARCHAR(100) NULL,
        DurationWeeks INT NOT NULL CONSTRAINT DF_Courses_Duration DEFAULT 4,
        Level NVARCHAR(50) NOT NULL CONSTRAINT DF_Courses_Level DEFAULT 'Beginner',
        Category NVARCHAR(50) NOT NULL CONSTRAINT DF_Courses_Category DEFAULT 'Frontend',
        Price DECIMAL(18,2) NOT NULL CONSTRAINT DF_Courses_Price DEFAULT 49.99,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_Courses_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_Courses_Instructor FOREIGN KEY (InstructorId) REFERENCES LMS.Users(UserId) ON DELETE SET NULL
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Courses') AND name = 'Category')
        ALTER TABLE LMS.Courses ADD Category NVARCHAR(50) NOT NULL CONSTRAINT DF_Courses_Category DEFAULT 'Frontend';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Courses') AND name = 'Price')
        ALTER TABLE LMS.Courses ADD Price DECIMAL(18,2) NOT NULL CONSTRAINT DF_Courses_Price DEFAULT 49.99;
END;
GO

-- 3.3 Enrollments Table
IF OBJECT_ID('LMS.Enrollments', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Enrollments (
        EnrollmentId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        CourseId INT NOT NULL,
        EnrolledAt DATETIME NOT NULL CONSTRAINT DF_Enrollments_EnrolledAt DEFAULT GETDATE(),
        ProgressPercentage INT NOT NULL CONSTRAINT DF_Enrollments_Progress DEFAULT 0,
        IsCompleted BIT NOT NULL CONSTRAINT DF_Enrollments_IsCompleted DEFAULT 0,
        CONSTRAINT FK_Enrollments_User FOREIGN KEY (UserId) REFERENCES LMS.Users(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_Enrollments_Course FOREIGN KEY (CourseId) REFERENCES LMS.Courses(CourseId) ON DELETE CASCADE,
        CONSTRAINT UQ_Enrollments_User_Course UNIQUE (UserId, CourseId)
    );
END;
GO

-- 3.4 Reviews Table
IF OBJECT_ID('LMS.Reviews', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Reviews (
        ReviewId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        CourseId INT NOT NULL,
        Rating INT NOT NULL CONSTRAINT DF_Reviews_Rating DEFAULT 5,
        Comment NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_Reviews_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_Reviews_User FOREIGN KEY (UserId) REFERENCES LMS.Users(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_Reviews_Course FOREIGN KEY (CourseId) REFERENCES LMS.Courses(CourseId) ON DELETE CASCADE
    );
END;
GO

-- 3.5 QuizQuestions Table
IF OBJECT_ID('LMS.QuizQuestions', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.QuizQuestions (
        QuestionId INT IDENTITY(1,1) PRIMARY KEY,
        CourseId INT NOT NULL,
        LessonNumber INT NOT NULL CONSTRAINT DF_Quiz_Lesson DEFAULT 1,
        QuestionText NVARCHAR(500) NOT NULL,
        OptionA NVARCHAR(250) NOT NULL,
        OptionB NVARCHAR(250) NOT NULL,
        OptionC NVARCHAR(250) NOT NULL,
        OptionD NVARCHAR(250) NOT NULL,
        CorrectOption CHAR(1) NOT NULL,
        Explanation NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_Quiz_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_QuizQuestions_Course FOREIGN KEY (CourseId) REFERENCES LMS.Courses(CourseId) ON DELETE CASCADE
    );
END;
GO

-- 3.6 Payments Table
IF OBJECT_ID('LMS.Payments', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Payments (
        PaymentId INT IDENTITY(1,1) PRIMARY KEY,
        InvoiceNumber NVARCHAR(50) NOT NULL UNIQUE,
        UserId INT NOT NULL,
        CourseId INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        PaymentMethod NVARCHAR(50) NOT NULL CONSTRAINT DF_Payments_Method DEFAULT 'Card',
        TransactionId NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL CONSTRAINT DF_Payments_Status DEFAULT 'Completed',
        PaymentDate DATETIME NOT NULL CONSTRAINT DF_Payments_Date DEFAULT GETDATE(),
        CONSTRAINT FK_Payments_User FOREIGN KEY (UserId) REFERENCES LMS.Users(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_Payments_Course FOREIGN KEY (CourseId) REFERENCES LMS.Courses(CourseId) ON DELETE CASCADE
    );
END;
GO

-- ============================================================================
-- 4. STORED PROCEDURES
-- ============================================================================

-- 4.1 Authenticate User
CREATE OR ALTER PROCEDURE LMS.sp_AuthenticateUser
    @Email NVARCHAR(150),
    @Password NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 UserId, FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt
    FROM LMS.Users
    WHERE LOWER(Email) = LOWER(@Email) AND Password = @Password;
END;
GO

-- 4.2 Register User
CREATE OR ALTER PROCEDURE LMS.sp_RegisterUser
    @FullName NVARCHAR(150),
    @Email NVARCHAR(150),
    @Password NVARCHAR(255),
    @Role NVARCHAR(50) = 'Student',
    @IsApproved BIT = 1,
    @ApprovalStatus NVARCHAR(50) = 'Approved'
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    OUTPUT INSERTED.UserId, INSERTED.FullName, INSERTED.Email, INSERTED.Password, INSERTED.Role, INSERTED.IsApproved, INSERTED.ApprovalStatus, INSERTED.CreatedAt
    VALUES (@FullName, @Email, @Password, @Role, @IsApproved, @ApprovalStatus, GETDATE());
END;
GO

-- 4.3 Get User By Id
CREATE OR ALTER PROCEDURE LMS.sp_GetUserById
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, FullName, Email, Role, IsApproved, ApprovalStatus, CreatedAt
    FROM LMS.Users
    WHERE UserId = @UserId;
END;
GO

-- 4.4 Change Password
CREATE OR ALTER PROCEDURE LMS.sp_ChangePassword
    @UserId INT,
    @CurrentPassword NVARCHAR(255),
    @NewPassword NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM LMS.Users WHERE UserId = @UserId AND Password = @CurrentPassword)
    BEGIN
        UPDATE LMS.Users SET Password = @NewPassword WHERE UserId = @UserId;
        SELECT 1 AS IsSuccess, 'Password updated successfully.' AS Message;
    END
    ELSE
    BEGIN
        SELECT 0 AS IsSuccess, 'Current password does not match.' AS Message;
    END
END;
GO

-- 4.5 Get Pending Instructors
CREATE OR ALTER PROCEDURE LMS.sp_GetPendingInstructors
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, FullName, Email, Role, IsApproved, ApprovalStatus, CreatedAt
    FROM LMS.Users
    WHERE LOWER(Role) = 'instructor' AND (IsApproved = 0 OR LOWER(ApprovalStatus) = 'pending')
    ORDER BY CreatedAt DESC;
END;
GO

-- 4.6 Review Instructor (Approve / Reject)
CREATE OR ALTER PROCEDURE LMS.sp_ReviewInstructor
    @UserId INT,
    @Action NVARCHAR(50) = 'Approve'
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @IsApproved BIT = 1;
    DECLARE @Status NVARCHAR(50) = 'Approved';
    IF LOWER(@Action) = 'reject'
    BEGIN
        SET @IsApproved = 0;
        SET @Status = 'Rejected';
    END

    UPDATE LMS.Users
    SET IsApproved = @IsApproved, ApprovalStatus = @Status
    WHERE UserId = @UserId;

    SELECT 1 AS IsSuccess, 
           CASE WHEN @IsApproved = 1 THEN 'Instructor approved successfully.' ELSE 'Instructor application rejected.' END AS Message;
END;
GO

-- 4.7 Get All Instructors
CREATE OR ALTER PROCEDURE LMS.sp_GetAllInstructors
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UserId, FullName, Email, Role, IsApproved, ApprovalStatus, CreatedAt
    FROM LMS.Users
    WHERE LOWER(Role) = 'instructor'
    ORDER BY FullName ASC;
END;
GO

-- 4.8 Enroll Course
CREATE OR ALTER PROCEDURE LMS.sp_EnrollCourse
    @UserId INT,
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @EnrollmentId INT;

    SELECT @EnrollmentId = EnrollmentId 
    FROM LMS.Enrollments 
    WHERE UserId = @UserId AND CourseId = @CourseId;

    IF @EnrollmentId IS NULL
    BEGIN
        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
        VALUES (@UserId, @CourseId, GETDATE(), 0, 0);

        SET @EnrollmentId = SCOPE_IDENTITY();
        SELECT 1 AS IsSuccess, 'Successfully enrolled in course.' AS Message, @EnrollmentId AS EnrollmentId;
    END
    ELSE
    BEGIN
        SELECT 1 AS IsSuccess, 'Already enrolled in course.' AS Message, @EnrollmentId AS EnrollmentId;
    END
END;
GO

-- 4.9 Check Enrollment Status
CREATE OR ALTER PROCEDURE LMS.sp_CheckEnrollment
    @UserId INT,
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 
        1 AS IsEnrolled,
        EnrollmentId,
        ProgressPercentage,
        CASE WHEN IsCompleted = 1 OR ProgressPercentage >= 100 THEN 'Completed' ELSE 'In Progress' END AS Status,
        EnrolledAt
    FROM LMS.Enrollments
    WHERE UserId = @UserId AND CourseId = @CourseId;
END;
GO

-- 4.10 Unenroll Course
CREATE OR ALTER PROCEDURE LMS.sp_UnenrollCourse
    @UserId INT,
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM LMS.Enrollments WHERE UserId = @UserId AND CourseId = @CourseId;
    SELECT 1 AS IsSuccess, 'Course unenrolled successfully.' AS Message;
END;
GO

-- 4.11 Student Dashboard
CREATE OR ALTER PROCEDURE LMS.sp_GetStudentDashboard
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.EnrollmentId, e.UserId, e.CourseId,
           c.Title AS Course,
           ISNULL(u.FullName, c.InstructorName) AS InstructorName,
           c.DurationWeeks, c.Level,
           e.ProgressPercentage AS Progress,
           CASE WHEN e.IsCompleted = 1 OR e.ProgressPercentage >= 100 THEN 'Completed' ELSE 'In Progress' END AS Status,
           e.EnrolledAt
    FROM LMS.Enrollments e
    JOIN LMS.Courses c ON e.CourseId = c.CourseId
    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
    WHERE e.UserId = @UserId
    ORDER BY e.EnrolledAt DESC;
END;
GO

-- 4.12 Invoices by User
CREATE OR ALTER PROCEDURE LMS.sp_GetInvoicesByUser
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.PaymentId, p.InvoiceNumber, p.Amount, p.PaymentMethod, p.TransactionId, p.Status, p.PaymentDate,
           c.CourseId, c.Title AS CourseTitle, ISNULL(u.FullName, c.InstructorName) AS InstructorName
    FROM LMS.Payments p
    JOIN LMS.Courses c ON p.CourseId = c.CourseId
    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
    WHERE p.UserId = @UserId
    ORDER BY p.PaymentDate DESC;
END;
GO

-- 4.13 Invoices by Instructor
CREATE OR ALTER PROCEDURE LMS.sp_GetInvoicesByInstructor
    @InstructorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.PaymentId, p.InvoiceNumber, p.Amount, p.PaymentMethod, p.TransactionId, p.Status, p.PaymentDate,
           s.FullName AS StudentName, s.Email AS StudentEmail,
           c.CourseId, c.Title AS CourseTitle
    FROM LMS.Payments p
    JOIN LMS.Courses c ON p.CourseId = c.CourseId
    JOIN LMS.Users s ON p.UserId = s.UserId
    WHERE c.InstructorId = @InstructorId
    ORDER BY p.PaymentDate DESC;
END;
GO

-- 4.14 All Invoices (Admin)
CREATE OR ALTER PROCEDURE LMS.sp_GetAllInvoices
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.PaymentId, p.InvoiceNumber, p.Amount, p.PaymentMethod, p.TransactionId, p.Status, p.PaymentDate,
           s.FullName AS StudentName, ISNULL(inst.FullName, c.InstructorName) AS InstructorName,
           c.CourseId, c.Title AS CourseTitle
    FROM LMS.Payments p
    JOIN LMS.Courses c ON p.CourseId = c.CourseId
    JOIN LMS.Users s ON p.UserId = s.UserId
    LEFT JOIN LMS.Users inst ON c.InstructorId = inst.UserId
    ORDER BY p.PaymentDate DESC;
END;
GO

-- 4.15 Invoice Details
CREATE OR ALTER PROCEDURE LMS.sp_GetInvoiceDetails
    @InvoiceNumber NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 p.PaymentId, p.InvoiceNumber, p.Amount, p.PaymentMethod, p.TransactionId, p.Status, p.PaymentDate,
           s.UserId AS StudentId, s.FullName AS StudentName, s.Email AS StudentEmail,
           c.CourseId, c.Title AS CourseTitle, c.Category, c.Level, c.DurationWeeks,
           inst.UserId AS InstructorId, ISNULL(inst.FullName, c.InstructorName) AS InstructorName, ISNULL(inst.Email, '') AS InstructorEmail
    FROM LMS.Payments p
    JOIN LMS.Courses c ON p.CourseId = c.CourseId
    JOIN LMS.Users s ON p.UserId = s.UserId
    LEFT JOIN LMS.Users inst ON c.InstructorId = inst.UserId
    WHERE p.InvoiceNumber = @InvoiceNumber;
END;
GO

-- 4.16 Process Payment
CREATE OR ALTER PROCEDURE LMS.sp_ProcessPayment
    @UserId INT,
    @CourseId INT,
    @Amount DECIMAL(18,2),
    @PaymentMethod NVARCHAR(50),
    @TransactionId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @InvoiceNumber NVARCHAR(50) = 'INV-' + CONVERT(NVARCHAR(8), GETDATE(), 112) + '-' + UPPER(SUBSTRING(REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', ''), 1, 6));

    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @UserId AND CourseId = @CourseId)
    BEGIN
        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
        VALUES (@UserId, @CourseId, GETDATE(), 0, 0);
    END;

    INSERT INTO LMS.Payments (InvoiceNumber, UserId, CourseId, Amount, PaymentMethod, TransactionId, Status, PaymentDate)
    VALUES (@InvoiceNumber, @UserId, @CourseId, @Amount, @PaymentMethod, @TransactionId, 'Completed', GETDATE());

    SELECT 1 AS IsSuccess, 'Payment processed successfully' AS Message, @InvoiceNumber AS InvoiceNumber;
END;
GO

-- 4.17 Create Quiz Question
CREATE OR ALTER PROCEDURE LMS.sp_CreateQuizQuestion
    @CourseId INT,
    @LessonNumber INT,
    @QuestionText NVARCHAR(500),
    @OptionA NVARCHAR(250),
    @OptionB NVARCHAR(250),
    @OptionC NVARCHAR(250),
    @OptionD NVARCHAR(250),
    @CorrectOption CHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO LMS.QuizQuestions (CourseId, LessonNumber, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectOption, CreatedAt)
    VALUES (@CourseId, @LessonNumber, @QuestionText, @OptionA, @OptionB, @OptionC, @OptionD, @CorrectOption, GETDATE());
    SELECT SCOPE_IDENTITY() AS QuestionId;
END;
GO

-- 4.18 Add Review
CREATE OR ALTER PROCEDURE LMS.sp_AddReview
    @CourseId INT,
    @UserId INT,
    @Rating INT,
    @Comment NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO LMS.Reviews (CourseId, UserId, Rating, Comment, CreatedAt)
    VALUES (@CourseId, @UserId, @Rating, @Comment, GETDATE());

    SELECT 1 AS IsSuccess, 'Review added successfully!' AS Message;
END;
GO

-- 4.19 Get Course Reviews
CREATE OR ALTER PROCEDURE LMS.sp_GetCourseReviews
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.ReviewId, r.CourseId, r.UserId,
           ISNULL(u.FullName, 'Learner') AS ReviewerName,
           r.Rating, r.Comment, r.CreatedAt
    FROM LMS.Reviews r
    LEFT JOIN LMS.Users u ON r.UserId = u.UserId
    WHERE r.CourseId = @CourseId
    ORDER BY r.CreatedAt DESC;
END;
GO

-- ============================================================================
-- 5. SEED DATA
-- ============================================================================

-- 5.1 Seed Admin
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'admin@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('System Admin', 'admin@lms.com', 'Admin@123', 'Admin', 1, 'Approved', GETDATE());
END
ELSE
BEGIN
    UPDATE LMS.Users 
    SET Role = 'Admin', IsApproved = 1, ApprovalStatus = 'Approved', Password = 'Admin@123'
    WHERE LOWER(Email) = 'admin@lms.com';
END;

-- 5.2 Seed Verified Instructor (A. Sharma)
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'asharma@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('A. Sharma', 'asharma@lms.com', 'Pass@123', 'Instructor', 1, 'Approved', GETDATE());
END;

-- 5.3 Seed Demo Pending Instructor (Pooja Mehta)
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'pmehta@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('Pooja Mehta', 'pmehta@lms.com', 'Pass@123', 'Instructor', 0, 'Pending', GETDATE());
END;

-- 5.4 Seed Demo Student (John Doe)
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'student@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('John Doe', 'student@lms.com', 'Pass@123', 'Student', 1, 'Approved', GETDATE());
END;
GO

-- 5.5 Seed Courses Catalog
DECLARE @SharmaId INT;
SELECT TOP 1 @SharmaId = UserId FROM LMS.Users WHERE LOWER(Email) = 'asharma@lms.com';

IF NOT EXISTS (SELECT 1 FROM LMS.Courses WHERE Title = 'Modern React 18 & TypeScript Masterclass')
BEGIN
    INSERT INTO LMS.Courses (Title, Description, InstructorId, InstructorName, DurationWeeks, Level, Category, Price, CreatedAt)
    VALUES (
        'Modern React 18 & TypeScript Masterclass',
        'Learn to build high-performance, accessible, and enterprise-scale single page applications with React 18, TypeScript, custom hooks, Redux Toolkit, and Vite.',
        @SharmaId,
        'A. Sharma',
        8,
        'Beginner',
        'Frontend',
        49.99,
        DATEADD(day, -30, GETDATE())
    );
END;

IF NOT EXISTS (SELECT 1 FROM LMS.Courses WHERE Title = 'Enterprise ASP.NET MVC & SQL Server Architecture')
BEGIN
    INSERT INTO LMS.Courses (Title, Description, InstructorId, InstructorName, DurationWeeks, Level, Category, Price, CreatedAt)
    VALUES (
        'Enterprise ASP.NET MVC & SQL Server Architecture',
        'Master monolithic and layered enterprise architecture using ASP.NET MVC 5, ADO.NET, Repository Patterns, stored procedures, authentication filters, and clean code principles.',
        @SharmaId,
        'A. Sharma',
        10,
        'Intermediate',
        'Backend',
        69.99,
        DATEADD(day, -20, GETDATE())
    );
END;

IF NOT EXISTS (SELECT 1 FROM LMS.Courses WHERE Title = 'Fullstack Next.js & Node.js Microservices')
BEGIN
    INSERT INTO LMS.Courses (Title, Description, InstructorId, InstructorName, DurationWeeks, Level, Category, Price, CreatedAt)
    VALUES (
        'Fullstack Next.js & Node.js Microservices',
        'Build complete end-to-end fullstack platforms combining Next.js App Router, serverless APIs, PostgreSQL, Redis caching, and real-time WebSockets.',
        @SharmaId,
        'A. Sharma',
        12,
        'Advanced',
        'Fullstack',
        89.99,
        DATEADD(day, -15, GETDATE())
    );
END;

IF NOT EXISTS (SELECT 1 FROM LMS.Courses WHERE Title = 'Docker, Kubernetes & Cloud CI/CD Pipelines')
BEGIN
    INSERT INTO LMS.Courses (Title, Description, InstructorId, InstructorName, DurationWeeks, Level, Category, Price, CreatedAt)
    VALUES (
        'Docker, Kubernetes & Cloud CI/CD Pipelines',
        'Containerize modern web applications, deploy microservices on Kubernetes clusters, automate test runs with GitHub Actions, and master zero-downtime rolling releases.',
        @SharmaId,
        'A. Sharma',
        6,
        'Intermediate',
        'DevOps',
        59.99,
        DATEADD(day, -10, GETDATE())
    );
END;
GO

-- 5.6 Seed Sample Quiz Questions for Course 1 (Modern React 18)
DECLARE @Course1Id INT;
SELECT TOP 1 @Course1Id = CourseId FROM LMS.Courses WHERE Title LIKE '%React 18%';

IF @Course1Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LMS.QuizQuestions WHERE CourseId = @Course1Id)
BEGIN
    INSERT INTO LMS.QuizQuestions (CourseId, LessonNumber, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectOption, CreatedAt)
    VALUES 
    (@Course1Id, 1, 'Which hook is used to manage mutable local state in functional React components?', 'useEffect', 'useState', 'useContext', 'useReducer', 'B', GETDATE()),
    (@Course1Id, 1, 'What is the primary role of Vite in a modern React application?', 'Database ORM', 'Build tool and dev server', 'Testing framework', 'CSS Preprocessor', 'B', GETDATE()),
    (@Course1Id, 2, 'How do props differ from state in React?', 'Props are mutable, state is read-only', 'Props are passed from parent, state is internally managed', 'Props cannot be primitives', 'State cannot trigger re-renders', 'B', GETDATE());
END;
GO

-- 5.7 Seed Sample Review for Course 1
DECLARE @CId INT, @SId INT;
SELECT TOP 1 @CId = CourseId FROM LMS.Courses WHERE Title LIKE '%React 18%';
SELECT TOP 1 @SId = UserId FROM LMS.Users WHERE LOWER(Email) = 'student@lms.com';

IF @CId IS NOT NULL AND @SId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM LMS.Reviews WHERE CourseId = @CId AND UserId = @SId)
BEGIN
    INSERT INTO LMS.Reviews (CourseId, UserId, Rating, Comment, CreatedAt)
    VALUES (@CId, @SId, 5, 'Outstanding course! The practical real-world architecture examples made React 18 concepts crystal clear.', GETDATE());
END;
GO

PRINT '=======================================================';
PRINT 'EduPulse LMS Database setup completed successfully!';
PRINT '=======================================================';


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

    SELECT p.ProfileId, p.UserId, p.PhoneNumber, p.Headline, p.City, p.Country, p.CurrentJobTitle, p.CurrentOrganization, p.TotalExperienceYears, p.PastTeachingExperience, p.PrimaryExpertise, p.Bio, p.HighestDegree, p.InstitutionName, p.GraduationYear, p.PlannedCourses, p.TargetLevel, p.ProposedPricing, p.ProfilePictureUrl, p.DigitalSignatureUrl, p.LinkedInUrl, p.GitHubOrPortfolioUrl, p.CompletionPercentage, p.ReviewedAt, p.ReviewedBy, p.UpdatedAt, u.FullName, u.Email, u.Role, u.IsApproved, u.ApprovalStatus, COALESCE(NULLIF(p.AdminFeedback, ''), u.AdminFeedback, '') AS AdminFeedback, u.AdminFeedback AS UserAdminFeedback, u.CreatedAt AS RegisteredAt
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



USE Practice;
GO

-- ============================================================================
-- 1. Create LMS.StudentQuizAttempts Table
-- ============================================================================
IF OBJECT_ID('LMS.StudentQuizAttempts', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.StudentQuizAttempts (
        AttemptId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        CourseId INT NOT NULL,
        LessonNumber INT NOT NULL,
        TotalQuestions INT NOT NULL,
        CorrectAnswers INT NOT NULL,
        ScorePercentage DECIMAL(5,2) NOT NULL,
        IsPassed BIT NOT NULL,
        AttemptedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_StudentQuizAttempts_User FOREIGN KEY (UserId) REFERENCES LMS.Users(UserId) ON DELETE CASCADE,
        CONSTRAINT FK_StudentQuizAttempts_Course FOREIGN KEY (CourseId) REFERENCES LMS.Courses(CourseId) ON DELETE CASCADE
    );
END;
GO

-- ============================================================================
-- 2. Stored Procedures for Quiz Attempts & Gradebook
-- ============================================================================

-- 2.1 sp_SaveQuizAttempt
CREATE OR ALTER PROCEDURE LMS.sp_SaveQuizAttempt
    @UserId INT,
    @CourseId INT,
    @LessonNumber INT,
    @TotalQuestions INT,
    @CorrectAnswers INT,
    @ScorePercentage DECIMAL(5,2),
    @IsPassed BIT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO LMS.StudentQuizAttempts (UserId, CourseId, LessonNumber, TotalQuestions, CorrectAnswers, ScorePercentage, IsPassed, AttemptedAt)
    VALUES (@UserId, @CourseId, @LessonNumber, @TotalQuestions, @CorrectAnswers, @ScorePercentage, @IsPassed, GETDATE());

    SELECT SCOPE_IDENTITY() AS AttemptId;
END;
GO

-- 2.2 sp_GetStudentQuizAttempts
CREATE OR ALTER PROCEDURE LMS.sp_GetStudentQuizAttempts
    @UserId INT,
    @CourseId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT a.AttemptId, a.UserId, a.CourseId, c.Title AS CourseTitle,
           a.LessonNumber, a.TotalQuestions, a.CorrectAnswers, a.ScorePercentage,
           a.IsPassed, a.AttemptedAt
    FROM LMS.StudentQuizAttempts a
    JOIN LMS.Courses c ON a.CourseId = c.CourseId
    WHERE a.UserId = @UserId
      AND (@CourseId IS NULL OR a.CourseId = @CourseId)
    ORDER BY a.AttemptedAt DESC;
END;
GO

-- 2.3 sp_GetCourseGradeSummary
-- Computes the weighted/cumulative score combining passed quizzes and approved assignments
CREATE OR ALTER PROCEDURE LMS.sp_GetCourseGradeSummary
    @UserId INT,
    @CourseId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Quiz metrics (best score per lesson)
    DECLARE @QuizAvg DECIMAL(5,2) = 0.00;
    DECLARE @TotalQuizzesTaken INT = 0;
    DECLARE @TotalQuizzesPassed INT = 0;

    SELECT 
        @TotalQuizzesTaken = COUNT(DISTINCT LessonNumber),
        @TotalQuizzesPassed = COUNT(DISTINCT CASE WHEN IsPassed = 1 THEN LessonNumber END),
        @QuizAvg = ISNULL(AVG(BestScore), 0.00)
    FROM (
        SELECT LessonNumber, MAX(ScorePercentage) AS BestScore, MAX(CAST(IsPassed AS INT)) AS IsPassed
        FROM LMS.StudentQuizAttempts
        WHERE UserId = @UserId AND CourseId = @CourseId
        GROUP BY LessonNumber
    ) q;

    -- Assignment metrics (approved marks)
    DECLARE @AssignmentAvg DECIMAL(5,2) = 0.00;
    DECLARE @TotalAssignmentsSubmitted INT = 0;
    DECLARE @TotalAssignmentsApproved INT = 0;
    DECLARE @TotalAssignmentsPending INT = 0;

    SELECT 
        @TotalAssignmentsSubmitted = COUNT(1),
        @TotalAssignmentsApproved = COUNT(CASE WHEN Status = 'Approved' THEN 1 END),
        @TotalAssignmentsPending = COUNT(CASE WHEN Status = 'Pending Review' THEN 1 END),
        @AssignmentAvg = ISNULL(AVG(CASE WHEN Status = 'Approved' AND MarksObtained IS NOT NULL THEN (MarksObtained / MaxMarks) * 100 END), 0.00)
    FROM LMS.StudentSubmissions
    WHERE UserId = @UserId AND CourseId = @CourseId;

    -- Combined Final Score Calculation
    DECLARE @FinalScore DECIMAL(5,2) = 0.00;

    IF @TotalQuizzesTaken > 0 AND @TotalAssignmentsApproved > 0
    BEGIN
        -- 40% Quiz weight + 60% Practical Assignments weight
        SET @FinalScore = (@QuizAvg * 0.40) + (@AssignmentAvg * 0.60);
    END
    ELSE IF @TotalQuizzesTaken > 0
    BEGIN
        SET @FinalScore = @QuizAvg;
    END
    ELSE IF @TotalAssignmentsApproved > 0
    BEGIN
        SET @FinalScore = @AssignmentAvg;
    END;

    -- Letter Grade
    DECLARE @LetterGrade NVARCHAR(10) = 'Incomplete';
    IF @FinalScore >= 90.00 SET @LetterGrade = 'A+';
    ELSE IF @FinalScore >= 80.00 SET @LetterGrade = 'A';
    ELSE IF @FinalScore >= 70.00 SET @LetterGrade = 'B';
    ELSE IF @FinalScore >= 60.00 SET @LetterGrade = 'C';
    ELSE IF @FinalScore > 0 SET @LetterGrade = 'D';

    SELECT 
        @UserId AS UserId,
        @CourseId AS CourseId,
        @QuizAvg AS QuizAverage,
        @TotalQuizzesTaken AS QuizzesTaken,
        @TotalQuizzesPassed AS QuizzesPassed,
        @AssignmentAvg AS AssignmentAverage,
        @TotalAssignmentsSubmitted AS AssignmentsSubmitted,
        @TotalAssignmentsApproved AS AssignmentsApproved,
        @TotalAssignmentsPending AS AssignmentsPending,
        @FinalScore AS FinalScore,
        @LetterGrade AS LetterGrade;
END;
GO

PRINT 'Student Gradebook & Quiz Attempts DB migration successfully executed!';

-- ============================================================================
-- 04_prevent_duplicate_submissions.sql
-- Enforces strictly no duplicate / re-submissions for the same student assignment
-- ============================================================================

USE Practice;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Create unique filtered index on (UserId, CourseId, ModuleId) where ModuleId IS NOT NULL
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_StudentSubmissions_User_Course_Module' AND object_id = OBJECT_ID('LMS.StudentSubmissions'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_StudentSubmissions_User_Course_Module
    ON LMS.StudentSubmissions(UserId, CourseId, ModuleId)
    WHERE ModuleId IS NOT NULL;
END
GO

-- 2. Create unique filtered index on (UserId, CourseId) where ModuleId IS NULL
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_StudentSubmissions_User_Course_NullModule' AND object_id = OBJECT_ID('LMS.StudentSubmissions'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_StudentSubmissions_User_Course_NullModule
    ON LMS.StudentSubmissions(UserId, CourseId)
    WHERE ModuleId IS NULL;
END
GO
-- ============================================================================
-- 05_admin_seller_verification.sql
-- Adds Admin Seller Verification, Review Feedback & Status Lookup Support
-- ============================================================================

USE Practice;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Add AdminFeedback and review tracking columns to LMS.InstructorProfiles
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.InstructorProfiles') AND name = 'AdminFeedback')
BEGIN
    ALTER TABLE LMS.InstructorProfiles ADD AdminFeedback NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.InstructorProfiles') AND name = 'ReviewedAt')
BEGIN
    ALTER TABLE LMS.InstructorProfiles ADD ReviewedAt DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.InstructorProfiles') AND name = 'ReviewedBy')
BEGIN
    ALTER TABLE LMS.InstructorProfiles ADD ReviewedBy INT NULL;
END
GO

-- 2. Add AdminFeedback to LMS.Users as well for fast status lookups
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'AdminFeedback')
BEGIN
    ALTER TABLE LMS.Users ADD AdminFeedback NVARCHAR(MAX) NULL;
END
GO

-- 3. Stored Procedure: sp_ReviewInstructorWithFeedback
CREATE OR ALTER PROCEDURE LMS.sp_ReviewInstructorWithFeedback
    @UserId INT,
    @Action NVARCHAR(50), -- 'Approve', 'RequestChanges', 'Reject'
    @AdminFeedback NVARCHAR(MAX) = NULL,
    @ReviewedBy INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IsApproved BIT = 0;
    DECLARE @Status NVARCHAR(50) = 'Pending';
    DECLARE @NormAction NVARCHAR(50) = LOWER(LTRIM(RTRIM(@Action)));

    IF @NormAction = 'approve'
    BEGIN
        SET @IsApproved = 1;
        SET @Status = 'Approved';
    END
    ELSE IF @NormAction IN ('requestchanges', 'needsrevision', 'actionrequired')
    BEGIN
        SET @IsApproved = 0;
        SET @Status = 'Action Required';
    END
    ELSE IF @NormAction = 'reject'
    BEGIN
        SET @IsApproved = 0;
        SET @Status = 'Rejected';
    END
    ELSE
    BEGIN
        SET @IsApproved = 0;
        SET @Status = 'Pending';
    END

    -- Update LMS.Users
    UPDATE LMS.Users
    SET IsApproved = @IsApproved,
        ApprovalStatus = @Status,
        AdminFeedback = @AdminFeedback
    WHERE UserId = @UserId;

    -- Ensure InstructorProfile exists & update feedback
    IF EXISTS (SELECT 1 FROM LMS.InstructorProfiles WHERE UserId = @UserId)
    BEGIN
        UPDATE LMS.InstructorProfiles
        SET AdminFeedback = @AdminFeedback,
            ReviewedAt = GETDATE(),
            ReviewedBy = @ReviewedBy
        WHERE UserId = @UserId;
    END
    ELSE
    BEGIN
        INSERT INTO LMS.InstructorProfiles (UserId, AdminFeedback, ReviewedAt, ReviewedBy, UpdatedAt)
        VALUES (@UserId, @AdminFeedback, GETDATE(), @ReviewedBy, GETDATE());
    END

    SELECT 1 AS IsSuccess, 
           CASE 
               WHEN @Status = 'Approved' THEN 'Instructor account approved successfully! The creator can now publish courses.'
               WHEN @Status = 'Action Required' THEN 'Improvement feedback sent to instructor. Status updated to Action Required.'
               WHEN @Status = 'Rejected' THEN 'Instructor application rejected with feedback.'
               ELSE 'Application status updated.'
           END AS Message;
END;
GO

-- 4. Stored Procedure: sp_GetInstructorVerificationDetails
CREATE OR ALTER PROCEDURE LMS.sp_GetInstructorVerificationDetails
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT u.UserId, u.FullName, u.Email, u.Role, u.IsApproved, u.ApprovalStatus, u.CreatedAt AS RegisteredAt,
           u.AdminFeedback AS UserAdminFeedback,
           p.ProfileId, p.PhoneNumber, p.Headline, p.City, p.Country,
           p.CurrentJobTitle, p.CurrentOrganization, p.TotalExperienceYears, p.PastTeachingExperience,
           p.PrimaryExpertise, p.Bio, p.HighestDegree, p.InstitutionName, p.GraduationYear,
           p.PlannedCourses, p.TargetLevel, p.ProposedPricing,
           p.ProfilePictureUrl, p.DigitalSignatureUrl, p.LinkedInUrl, p.GitHubOrPortfolioUrl,
           ISNULL(p.CompletionPercentage, 0) AS CompletionPercentage,
           ISNULL(p.AdminFeedback, u.AdminFeedback) AS AdminFeedback,
           p.ReviewedAt, p.ReviewedBy,
           rev.FullName AS ReviewerName,
           (SELECT COUNT(1) FROM LMS.Courses c WHERE c.InstructorId = u.UserId) AS PublishedCoursesCount
    FROM LMS.Users u
    LEFT JOIN LMS.InstructorProfiles p ON u.UserId = p.UserId
    LEFT JOIN LMS.Users rev ON p.ReviewedBy = rev.UserId
    WHERE u.UserId = @UserId;
END;
GO

-- 5. Stored Procedure: sp_GetAllInstructorsWithProfile
CREATE OR ALTER PROCEDURE LMS.sp_GetAllInstructorsWithProfile
    @StatusFilter NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT u.UserId, u.FullName, u.Email, u.Role, u.IsApproved, u.ApprovalStatus, u.CreatedAt AS RegisteredAt,
           p.ProfileId, p.PhoneNumber, p.Headline, p.CurrentJobTitle, p.CurrentOrganization,
           p.TotalExperienceYears, p.PrimaryExpertise, p.HighestDegree, p.InstitutionName,
           p.ProposedPricing, p.ProfilePictureUrl, p.DigitalSignatureUrl,
           ISNULL(p.CompletionPercentage, 0) AS CompletionPercentage,
           ISNULL(p.AdminFeedback, u.AdminFeedback) AS AdminFeedback,
           p.ReviewedAt, rev.FullName AS ReviewerName,
           (SELECT COUNT(1) FROM LMS.Courses c WHERE c.InstructorId = u.UserId) AS PublishedCoursesCount
    FROM LMS.Users u
    LEFT JOIN LMS.InstructorProfiles p ON u.UserId = p.UserId
    LEFT JOIN LMS.Users rev ON p.ReviewedBy = rev.UserId
    WHERE u.Role = 'Instructor'
      AND (@StatusFilter IS NULL OR @StatusFilter = 'All' 
           OR LOWER(u.ApprovalStatus) = LOWER(@StatusFilter)
           OR (@StatusFilter = 'Pending' AND (u.IsApproved = 0 AND (u.ApprovalStatus IS NULL OR LOWER(u.ApprovalStatus) = 'pending'))))
    ORDER BY 
        CASE 
            WHEN LOWER(u.ApprovalStatus) = 'pending' THEN 1 
            WHEN LOWER(u.ApprovalStatus) = 'action required' THEN 2
            WHEN LOWER(u.ApprovalStatus) = 'approved' THEN 3
            ELSE 4 
        END,
        u.CreatedAt DESC;
END;
GO

-- 6. Stored Procedure: sp_GetInstructorApplicationStatusByEmail
CREATE OR ALTER PROCEDURE LMS.sp_GetInstructorApplicationStatusByEmail
    @Email NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 
           u.UserId, u.FullName, u.Email, u.Role, u.IsApproved, 
           ISNULL(u.ApprovalStatus, 'Pending') AS ApprovalStatus, 
           u.CreatedAt AS RegisteredAt,
           p.ProfileId, p.Headline, p.CurrentJobTitle, p.CurrentOrganization,
           p.PlannedCourses, p.ProposedPricing,
           p.ProfilePictureUrl, p.DigitalSignatureUrl,
           ISNULL(p.CompletionPercentage, 0) AS CompletionPercentage,
           ISNULL(p.AdminFeedback, u.AdminFeedback) AS AdminFeedback,
           p.ReviewedAt, rev.FullName AS ReviewerName
    FROM LMS.Users u
    LEFT JOIN LMS.InstructorProfiles p ON u.UserId = p.UserId
    LEFT JOIN LMS.Users rev ON p.ReviewedBy = rev.UserId
    WHERE LOWER(LTRIM(RTRIM(u.Email))) = LOWER(LTRIM(RTRIM(@Email)))
      AND u.Role = 'Instructor';
END;
GO
