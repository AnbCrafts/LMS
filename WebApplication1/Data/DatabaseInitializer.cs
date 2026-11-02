using System;
using System.Data.SqlClient;

namespace WebApplication1.Data
{
    public static class DatabaseInitializer
    {
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        public static void Initialize()
        {
            if (_isInitialized) return;

            lock (_lock)
            {
                if (_isInitialized) return;

                try
                {
                    ExecuteSetupScript();
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Database initialization error: " + ex.Message);
                    // Do not crash the entire app; repositories have secondary fallbacks
                }
            }
        }

        private static void ExecuteSetupScript()
        {
            string sql = @"
-- 1. Ensure Schema
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'LMS')
BEGIN
    EXEC('CREATE SCHEMA LMS');
END;

-- 2. Ensure LMS.Users Table
IF OBJECT_ID('LMS.Users', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Users (
        UserId INT IDENTITY(1,1) PRIMARY KEY,
        FullName NVARCHAR(150) NOT NULL,
        Email NVARCHAR(150) NOT NULL UNIQUE,
        Password NVARCHAR(255) NOT NULL,
        Role NVARCHAR(50) NOT NULL DEFAULT 'Student',
        IsApproved BIT NOT NULL DEFAULT 1,
        ApprovalStatus NVARCHAR(50) NOT NULL DEFAULT 'Approved',
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'Role')
        ALTER TABLE LMS.Users ADD Role NVARCHAR(50) NOT NULL DEFAULT 'Student';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'IsApproved')
        ALTER TABLE LMS.Users ADD IsApproved BIT NOT NULL DEFAULT 1;
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'ApprovalStatus')
        ALTER TABLE LMS.Users ADD ApprovalStatus NVARCHAR(50) NOT NULL DEFAULT 'Approved';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'CreatedAt')
        ALTER TABLE LMS.Users ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE();
END;

-- 3. Ensure LMS.Courses Table
IF OBJECT_ID('LMS.Courses', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Courses (
        CourseId INT IDENTITY(1,1) PRIMARY KEY,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        InstructorId INT NOT NULL,
        InstructorName NVARCHAR(100) NULL,
        DurationWeeks INT NOT NULL DEFAULT 4,
        Level NVARCHAR(50) NOT NULL DEFAULT 'Beginner',
        Category NVARCHAR(50) NOT NULL DEFAULT 'Frontend',
        Price DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Courses') AND name = 'Category')
        ALTER TABLE LMS.Courses ADD Category NVARCHAR(50) NOT NULL DEFAULT 'Frontend';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Courses') AND name = 'Price')
        ALTER TABLE LMS.Courses ADD Price DECIMAL(18,2) NOT NULL DEFAULT 0.00;
END;

-- 4. Ensure LMS.Enrollments Table
IF OBJECT_ID('LMS.Enrollments', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Enrollments (
        EnrollmentId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        CourseId INT NOT NULL,
        EnrolledAt DATETIME NOT NULL DEFAULT GETDATE(),
        ProgressPercentage INT NOT NULL DEFAULT 0,
        IsCompleted BIT NOT NULL DEFAULT 0
    );
END;

-- 5. Ensure LMS.Reviews Table
IF OBJECT_ID('LMS.Reviews', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Reviews (
        ReviewId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        CourseId INT NOT NULL,
        Rating INT NOT NULL DEFAULT 5,
        Comment NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END;

-- 6. Ensure LMS.QuizQuestions Table
IF OBJECT_ID('LMS.QuizQuestions', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.QuizQuestions (
        QuestionId INT IDENTITY(1,1) PRIMARY KEY,
        CourseId INT NOT NULL,
        LessonNumber INT NOT NULL DEFAULT 1,
        QuestionText NVARCHAR(MAX) NOT NULL,
        OptionA NVARCHAR(500) NOT NULL,
        OptionB NVARCHAR(500) NOT NULL,
        OptionC NVARCHAR(500) NOT NULL,
        OptionD NVARCHAR(500) NOT NULL,
        CorrectOption CHAR(1) NOT NULL,
        Explanation NVARCHAR(MAX) NULL
    );
END;

-- 7. Ensure LMS.Payments Table
IF OBJECT_ID('LMS.Payments', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.Payments (
        PaymentId INT IDENTITY(1,1) PRIMARY KEY,
        InvoiceNumber NVARCHAR(50) NOT NULL UNIQUE,
        UserId INT NOT NULL,
        CourseId INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Card',
        TransactionId NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Completed',
        PaymentDate DATETIME NOT NULL DEFAULT GETDATE()
    );
END;

-- 8. Seed Default System Admin (admin@lms.com / Admin@123)
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

-- 9. Seed Demo Verified Instructor (asharma@lms.com / Pass@123)
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'asharma@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('A. Sharma', 'asharma@lms.com', 'Pass@123', 'Instructor', 1, 'Approved', GETDATE());
END;

-- 10. Seed Demo Pending Instructor (pmehta@lms.com / Pass@123)
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'pmehta@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('Pooja Mehta', 'pmehta@lms.com', 'Pass@123', 'Instructor', 0, 'Pending', GETDATE());
END;

-- 11. Seed Demo Student (student@lms.com / Pass@123)
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'student@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('John Doe', 'student@lms.com', 'Pass@123', 'Student', 1, 'Approved', GETDATE());
END;

-- 12. Ensure non-seed instructors who recently registered are set to Pending so Admin can verify them
UPDATE LMS.Users 
SET IsApproved = 0, ApprovalStatus = 'Pending' 
WHERE Role = 'Instructor' 
  AND LOWER(Email) NOT IN ('asharma@lms.com') 
  AND (ApprovalStatus IS NULL OR ApprovalStatus = '' OR (ApprovalStatus = 'Approved' AND UserId NOT IN (SELECT DISTINCT InstructorId FROM LMS.Courses)));
";

            DbHelper.ExecuteNonQuery(sql);
        }
    }
}
