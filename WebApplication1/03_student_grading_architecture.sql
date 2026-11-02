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
