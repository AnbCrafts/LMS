USE Practice;
GO

-- 1. Ensure Columns in LMS.Users
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'IsApproved')
BEGIN
    ALTER TABLE LMS.Users ADD IsApproved BIT NOT NULL CONSTRAINT DF_Users_IsApproved DEFAULT 1;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Users') AND name = 'ApprovalStatus')
BEGIN
    ALTER TABLE LMS.Users ADD ApprovalStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_Users_ApprovalStatus DEFAULT 'Approved';
END
GO

-- 2. Seed Admin User
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
END
GO

-- 3. Seed Demo Pending Instructor
IF NOT EXISTS (SELECT 1 FROM LMS.Users WHERE LOWER(Email) = 'pmehta@lms.com')
BEGIN
    INSERT INTO LMS.Users (FullName, Email, Password, Role, IsApproved, ApprovalStatus, CreatedAt)
    VALUES ('Pooja Mehta', 'pmehta@lms.com', 'Pass@123', 'Instructor', 0, 'Pending', GETDATE());
END
GO

-- 4. Set newly registered instructor (Anubhaw Kumar Gupta) to Pending so Admin can verify him
UPDATE LMS.Users 
SET IsApproved = 0, ApprovalStatus = 'Pending' 
WHERE LOWER(Email) = 'anubhawgupta664@gmail.com';
GO

-- 5. Ensure Columns in LMS.Courses
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Courses') AND name = 'Category')
BEGIN
    ALTER TABLE LMS.Courses ADD Category NVARCHAR(50) NOT NULL CONSTRAINT DF_Courses_Category DEFAULT 'Frontend';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('LMS.Courses') AND name = 'Price')
BEGIN
    ALTER TABLE LMS.Courses ADD Price DECIMAL(18,2) NOT NULL CONSTRAINT DF_Courses_Price DEFAULT 49.99;
END
GO

-- 6. Create LMS.Payments Table
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
END
GO

-- 7. Create LMS.Reviews Table
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
END
GO

-- 8. Create LMS.QuizQuestions Table
IF OBJECT_ID('LMS.QuizQuestions', 'U') IS NULL
BEGIN
    CREATE TABLE LMS.QuizQuestions (
        QuestionId INT IDENTITY(1,1) PRIMARY KEY,
        CourseId INT NOT NULL,
        LessonNumber INT NOT NULL DEFAULT 1,
        QuestionText NVARCHAR(500) NOT NULL,
        OptionA NVARCHAR(250) NOT NULL,
        OptionB NVARCHAR(250) NOT NULL,
        OptionC NVARCHAR(250) NOT NULL,
        OptionD NVARCHAR(250) NOT NULL,
        CorrectOption CHAR(1) NOT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO
