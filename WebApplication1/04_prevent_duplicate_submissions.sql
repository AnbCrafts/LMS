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
