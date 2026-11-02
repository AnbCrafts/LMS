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
