USE Practice;
GO

-- 1. sp_GetPendingInstructors
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

-- 2. sp_ReviewInstructor
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

-- 3. sp_GetAllInstructors
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

-- 4. sp_GetUserById
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

-- 5. sp_GetInvoicesByUser
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

-- 6. sp_GetInvoicesByInstructor
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

-- 7. sp_GetAllInvoices
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

-- 8. sp_GetInvoiceDetails
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

-- 9. sp_ProcessPayment
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

-- 10. sp_CreateQuizQuestion
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

-- 11. sp_AddReview
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

-- 12. sp_GetCourseReviews
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
