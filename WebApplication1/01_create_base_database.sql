-- ============================================================================
-- SCRIPT 1 OF 3: 01_create_base_database.sql (Base Database, Tables & Seed Data)
-- Database: Practice
-- Description: Creates the LMS schema, primary tables (Users, Courses, Enrollments),
--              missing core stored procedures, and initial seed courses & demo users.
-- ============================================================================

USE Practice;
GO

-- 1. Ensure LMS Schema exists
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'LMS')
BEGIN
    EXEC('CREATE SCHEMA LMS');
END;
GO

-- 2. Create LMS.Users Table
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
END;
GO

-- 3. Create LMS.Courses Table
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
END;
GO

-- 4. Create LMS.Enrollments Table
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

-- 5. Create Core Authentication & User Stored Procedures
-- 5.1 Authenticate User
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

-- 5.2 Register User
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

-- 5.3 Change Password
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

-- 6. Create Course Enrollment & Student Dashboard Stored Procedures
-- 6.1 Enroll Course
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

-- 6.2 Check Enrollment Status
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

-- 6.3 Unenroll Course
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

-- 6.4 Student Dashboard
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

-- 7. Seed Initial Instructors and Students
-- Seed Verified Instructor (A. Sharma)
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'asharma@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('A. Sharma', 'asharma@lms.com', 'Pass@123', 'Instructor', 1, 'Approved', GETDATE());
END;

-- Seed Demo Student (John Doe)
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'student@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('John Doe', 'student@lms.com', 'Pass@123', 'Student', 1, 'Approved', GETDATE());
END;
GO

-- 8. Seed Default Rich Courses Catalog
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
