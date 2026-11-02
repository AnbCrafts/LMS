using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public class InstructorRepository : IInstructorRepository
    {
        public InstructorProfile GetProfile(int userId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var p = new[] { new SqlParameter("@UserId", userId) };
                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetInstructorProfile", p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapRowToProfile(dt.Rows[0]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetProfile error: " + ex.Message);
            }

            return new InstructorProfile { UserId = userId, CompletionPercentage = 0 };
        }

        public InstructorProfile SaveProfile(InstructorProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            DatabaseInitializer.Initialize();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@UserId", profile.UserId),
                    new SqlParameter("@PhoneNumber", (object)profile.PhoneNumber ?? DBNull.Value),
                    new SqlParameter("@Headline", (object)profile.Headline ?? DBNull.Value),
                    new SqlParameter("@City", (object)profile.City ?? DBNull.Value),
                    new SqlParameter("@Country", (object)profile.Country ?? DBNull.Value),
                    new SqlParameter("@CurrentJobTitle", (object)profile.CurrentJobTitle ?? DBNull.Value),
                    new SqlParameter("@CurrentOrganization", (object)profile.CurrentOrganization ?? DBNull.Value),
                    new SqlParameter("@TotalExperienceYears", profile.TotalExperienceYears.HasValue ? (object)profile.TotalExperienceYears.Value : DBNull.Value),
                    new SqlParameter("@PastTeachingExperience", (object)profile.PastTeachingExperience ?? DBNull.Value),
                    new SqlParameter("@PrimaryExpertise", (object)profile.PrimaryExpertise ?? DBNull.Value),
                    new SqlParameter("@Bio", (object)profile.Bio ?? DBNull.Value),
                    new SqlParameter("@HighestDegree", (object)profile.HighestDegree ?? DBNull.Value),
                    new SqlParameter("@InstitutionName", (object)profile.InstitutionName ?? DBNull.Value),
                    new SqlParameter("@GraduationYear", profile.GraduationYear.HasValue ? (object)profile.GraduationYear.Value : DBNull.Value),
                    new SqlParameter("@PlannedCourses", (object)profile.PlannedCourses ?? DBNull.Value),
                    new SqlParameter("@TargetLevel", (object)profile.TargetLevel ?? "Beginner"),
                    new SqlParameter("@ProposedPricing", profile.ProposedPricing.HasValue ? (object)profile.ProposedPricing.Value : 49.99m),
                    new SqlParameter("@ProfilePictureUrl", (object)profile.ProfilePictureUrl ?? DBNull.Value),
                    new SqlParameter("@DigitalSignatureUrl", (object)profile.DigitalSignatureUrl ?? DBNull.Value),
                    new SqlParameter("@LinkedInUrl", (object)profile.LinkedInUrl ?? DBNull.Value),
                    new SqlParameter("@GitHubOrPortfolioUrl", (object)profile.GitHubOrPortfolioUrl ?? DBNull.Value)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_SaveInstructorProfile", p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapRowToProfile(dt.Rows[0]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SaveProfile error: " + ex.Message);
            }

            return GetProfile(profile.UserId);
        }

        public bool CheckProfileCompletionGate(int userId)
        {
            var profile = GetProfile(userId);
            return profile != null && profile.CompletionPercentage >= 90;
        }

        public IEnumerable<CourseModule> GetModulesByCourse(int courseId)
        {
            DatabaseInitializer.Initialize();
            var list = new List<CourseModule>();

            try
            {
                var p = new[] { new SqlParameter("@CourseId", courseId) };
                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetCourseModules", p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapRowToModule(row));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetModulesByCourse error: " + ex.Message);
            }

            return list;
        }

        public CourseModule GetModuleById(int moduleId, int courseId)
        {
            var modules = GetModulesByCourse(courseId);
            foreach (var m in modules)
            {
                if (m.ModuleId == moduleId) return m;
            }
            return null;
        }

        public OperationResult SaveModule(CourseModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            DatabaseInitializer.Initialize();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@ModuleId", module.ModuleId),
                    new SqlParameter("@CourseId", module.CourseId),
                    new SqlParameter("@ModuleNumber", module.ModuleNumber),
                    new SqlParameter("@Title", (object)module.Title ?? DBNull.Value),
                    new SqlParameter("@Description", (object)module.Description ?? DBNull.Value),
                    new SqlParameter("@DurationMinutes", module.DurationMinutes),
                    new SqlParameter("@ContentMarkdown", (object)module.ContentMarkdown ?? DBNull.Value),
                    new SqlParameter("@VideoOrResourceUrl", (object)module.VideoOrResourceUrl ?? DBNull.Value),
                    new SqlParameter("@AssignmentPrompt", (object)module.AssignmentPrompt ?? DBNull.Value)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_SaveCourseModule", p);
                int newId = dt != null && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["ModuleId"]) : module.ModuleId;

                return OperationResult.Success("Module saved successfully.", newId);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to save module: " + ex.Message);
            }
        }

        public OperationResult DeleteModule(int moduleId, int courseId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@ModuleId", moduleId),
                    new SqlParameter("@CourseId", courseId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_DeleteCourseModule", p);
                return OperationResult.Success("Module deleted successfully.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to delete module: " + ex.Message);
            }
        }

        public IEnumerable<StudentSubmission> GetSubmissionsForInstructor(int instructorId)
        {
            DatabaseInitializer.Initialize();
            var list = new List<StudentSubmission>();

            try
            {
                var p = new[] { new SqlParameter("@InstructorId", instructorId) };
                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetSubmissionsForInstructor", p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapRowToSubmission(row));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetSubmissionsForInstructor error: " + ex.Message);
            }

            return list;
        }

        public OperationResult GradeSubmission(int submissionId, decimal marks, string feedback, string status, int reviewedBy)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@SubmissionId", submissionId),
                    new SqlParameter("@MarksObtained", marks),
                    new SqlParameter("@InstructorFeedback", (object)feedback ?? DBNull.Value),
                    new SqlParameter("@Status", (object)status ?? "Approved"),
                    new SqlParameter("@ReviewedBy", reviewedBy)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GradeSubmission", p);
                return OperationResult.Success("Student submission successfully reviewed and marks awarded.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to grade submission: " + ex.Message);
            }
        }

        public IEnumerable<StudentSubmission> GetStudentSubmissions(int userId, int? courseId = null)
        {
            DatabaseInitializer.Initialize();
            var list = new List<StudentSubmission>();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId.HasValue ? (object)courseId.Value : DBNull.Value)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetStudentSubmissions", p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapRowToSubmission(row));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetStudentSubmissions error: " + ex.Message);
            }

            return list;
        }

        public OperationResult SubmitAssignment(StudentSubmission submission)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));
            DatabaseInitializer.Initialize();

            try
            {
                // PREVENT RE-SUBMISSION: Check if user has already submitted for this course module
                string checkSql = @"
                    SELECT COUNT(1) 
                    FROM LMS.StudentSubmissions 
                    WHERE UserId = @UserId 
                      AND CourseId = @CourseId 
                      AND ((@ModuleId IS NOT NULL AND ModuleId = @ModuleId) 
                           OR (@ModuleId IS NULL AND ModuleId IS NULL));";

                var checkParams = new[]
                {
                    new SqlParameter("@UserId", submission.UserId),
                    new SqlParameter("@CourseId", submission.CourseId),
                    new SqlParameter("@ModuleId", submission.ModuleId.HasValue ? (object)submission.ModuleId.Value : DBNull.Value)
                };

                int existingCount = Convert.ToInt32(DbHelper.ExecuteScalar(checkSql, checkParams));
                if (existingCount > 0)
                {
                    return OperationResult.Failure("You have already submitted this assignment. Re-submissions are strictly not permitted.");
                }

                string sql = @"
                    INSERT INTO LMS.StudentSubmissions (CourseId, ModuleId, UserId, SubmissionTitle, SubmissionText, SubmissionFileUrl, SubmittedAt, Status, MaxMarks)
                    VALUES (@CourseId, @ModuleId, @UserId, @SubmissionTitle, @SubmissionText, @SubmissionFileUrl, GETDATE(), 'Pending Review', 100.00);
                    SELECT SCOPE_IDENTITY();";

                var p = new[]
                {
                    new SqlParameter("@CourseId", submission.CourseId),
                    new SqlParameter("@ModuleId", submission.ModuleId.HasValue ? (object)submission.ModuleId.Value : DBNull.Value),
                    new SqlParameter("@UserId", submission.UserId),
                    new SqlParameter("@SubmissionTitle", (object)submission.SubmissionTitle ?? "Assignment Submission"),
                    new SqlParameter("@SubmissionText", (object)submission.SubmissionText ?? DBNull.Value),
                    new SqlParameter("@SubmissionFileUrl", (object)submission.SubmissionFileUrl ?? DBNull.Value)
                };

                object scalar = DbHelper.ExecuteScalar(sql, p);
                int newId = Convert.ToInt32(scalar);
                return OperationResult.Success("Assignment submitted successfully. Your submission is pending instructor review.", newId);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("UQ_StudentSubmissions") || ex.Message.Contains("duplicate key"))
                {
                    return OperationResult.Failure("You have already submitted this assignment. Re-submissions are strictly not permitted.");
                }
                return OperationResult.Failure("Failed to submit assignment: " + ex.Message);
            }
        }

        public OperationResult SaveQuizAttempt(StudentQuizAttempt attempt)
        {
            if (attempt == null) throw new ArgumentNullException(nameof(attempt));
            DatabaseInitializer.Initialize();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@UserId", attempt.UserId),
                    new SqlParameter("@CourseId", attempt.CourseId),
                    new SqlParameter("@LessonNumber", attempt.LessonNumber),
                    new SqlParameter("@TotalQuestions", attempt.TotalQuestions),
                    new SqlParameter("@CorrectAnswers", attempt.CorrectAnswers),
                    new SqlParameter("@ScorePercentage", attempt.ScorePercentage),
                    new SqlParameter("@IsPassed", attempt.IsPassed)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_SaveQuizAttempt", p);
                int newId = dt != null && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["AttemptId"]) : 0;
                return OperationResult.Success("Quiz attempt recorded successfully.", newId);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to record quiz attempt: " + ex.Message);
            }
        }

        public IEnumerable<StudentQuizAttempt> GetStudentQuizAttempts(int userId, int? courseId = null)
        {
            DatabaseInitializer.Initialize();
            var list = new List<StudentQuizAttempt>();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId.HasValue ? (object)courseId.Value : DBNull.Value)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetStudentQuizAttempts", p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new StudentQuizAttempt
                        {
                            AttemptId = Convert.ToInt32(row["AttemptId"]),
                            UserId = Convert.ToInt32(row["UserId"]),
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            CourseTitle = row["CourseTitle"].ToString(),
                            LessonNumber = Convert.ToInt32(row["LessonNumber"]),
                            TotalQuestions = Convert.ToInt32(row["TotalQuestions"]),
                            CorrectAnswers = Convert.ToInt32(row["CorrectAnswers"]),
                            ScorePercentage = Convert.ToDecimal(row["ScorePercentage"]),
                            IsPassed = Convert.ToBoolean(row["IsPassed"]),
                            AttemptedAt = Convert.ToDateTime(row["AttemptedAt"])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetStudentQuizAttempts error: " + ex.Message);
            }

            return list;
        }

        public CourseGradeSummary GetCourseGradeSummary(int userId, int courseId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetCourseGradeSummary", p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    return new CourseGradeSummary
                    {
                        UserId = Convert.ToInt32(row["UserId"]),
                        CourseId = Convert.ToInt32(row["CourseId"]),
                        QuizAverage = Convert.ToDecimal(row["QuizAverage"]),
                        QuizzesTaken = Convert.ToInt32(row["QuizzesTaken"]),
                        QuizzesPassed = Convert.ToInt32(row["QuizzesPassed"]),
                        AssignmentAverage = Convert.ToDecimal(row["AssignmentAverage"]),
                        AssignmentsSubmitted = Convert.ToInt32(row["AssignmentsSubmitted"]),
                        AssignmentsApproved = Convert.ToInt32(row["AssignmentsApproved"]),
                        AssignmentsPending = Convert.ToInt32(row["AssignmentsPending"]),
                        FinalScore = Convert.ToDecimal(row["FinalScore"]),
                        LetterGrade = row["LetterGrade"].ToString()
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetCourseGradeSummary error: " + ex.Message);
            }

            return new CourseGradeSummary { UserId = userId, CourseId = courseId };
        }

        public InstructorProfile GetInstructorVerificationDetails(int userId)
        {
            DatabaseInitializer.Initialize();
            try
            {
                var p = new[] { new SqlParameter("@UserId", userId) };
                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetInstructorVerificationDetails", p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapRowToProfile(dt.Rows[0]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetInstructorVerificationDetails error: " + ex.Message);
            }

            return null;
        }

        public IEnumerable<InstructorProfile> GetAllInstructorsWithProfiles(string statusFilter = null)
        {
            DatabaseInitializer.Initialize();
            var list = new List<InstructorProfile>();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@StatusFilter", string.IsNullOrWhiteSpace(statusFilter) ? (object)DBNull.Value : statusFilter.Trim())
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetAllInstructorsWithProfile", p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapRowToProfile(row));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetAllInstructorsWithProfiles error: " + ex.Message);
            }

            return list;
        }

        public OperationResult ReviewInstructorWithFeedback(int userId, string action, string feedback, int? reviewedBy)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var p = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@Action", (object)action ?? "Approve"),
                    new SqlParameter("@AdminFeedback", string.IsNullOrWhiteSpace(feedback) ? (object)DBNull.Value : feedback.Trim()),
                    new SqlParameter("@ReviewedBy", reviewedBy.HasValue ? (object)reviewedBy.Value : DBNull.Value)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_ReviewInstructorWithFeedback", p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    bool isSuccess = Convert.ToInt32(dt.Rows[0]["IsSuccess"]) == 1;
                    string msg = dt.Rows[0]["Message"].ToString();
                    return new OperationResult { IsSuccess = isSuccess, Message = msg };
                }

                return OperationResult.Success("Instructor review completed successfully.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to review instructor: " + ex.Message);
            }
        }

        public InstructorProfile GetInstructorApplicationStatusByEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            DatabaseInitializer.Initialize();

            try
            {
                var p = new[] { new SqlParameter("@Email", email.Trim()) };
                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetInstructorApplicationStatusByEmail", p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapRowToProfile(dt.Rows[0]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetInstructorApplicationStatusByEmail error: " + ex.Message);
            }

            return null;
        }

        private InstructorProfile MapRowToProfile(DataRow row)
        {
            return new InstructorProfile
            {
                ProfileId = row.Table.Columns.Contains("ProfileId") && row["ProfileId"] != DBNull.Value ? Convert.ToInt32(row["ProfileId"]) : 0,
                UserId = Convert.ToInt32(row["UserId"]),
                FullName = row.Table.Columns.Contains("FullName") && row["FullName"] != DBNull.Value ? row["FullName"].ToString() : string.Empty,
                Email = row.Table.Columns.Contains("Email") && row["Email"] != DBNull.Value ? row["Email"].ToString() : string.Empty,
                Role = row.Table.Columns.Contains("Role") && row["Role"] != DBNull.Value ? row["Role"].ToString() : "Instructor",
                IsApproved = row.Table.Columns.Contains("IsApproved") && row["IsApproved"] != DBNull.Value ? Convert.ToBoolean(row["IsApproved"]) : false,
                ApprovalStatus = row.Table.Columns.Contains("ApprovalStatus") && row["ApprovalStatus"] != DBNull.Value ? row["ApprovalStatus"].ToString() : "Pending",
                PhoneNumber = row.Table.Columns.Contains("PhoneNumber") && row["PhoneNumber"] != DBNull.Value ? row["PhoneNumber"].ToString() : string.Empty,
                Headline = row.Table.Columns.Contains("Headline") && row["Headline"] != DBNull.Value ? row["Headline"].ToString() : string.Empty,
                City = row.Table.Columns.Contains("City") && row["City"] != DBNull.Value ? row["City"].ToString() : string.Empty,
                Country = row.Table.Columns.Contains("Country") && row["Country"] != DBNull.Value ? row["Country"].ToString() : string.Empty,
                CurrentJobTitle = row.Table.Columns.Contains("CurrentJobTitle") && row["CurrentJobTitle"] != DBNull.Value ? row["CurrentJobTitle"].ToString() : string.Empty,
                CurrentOrganization = row.Table.Columns.Contains("CurrentOrganization") && row["CurrentOrganization"] != DBNull.Value ? row["CurrentOrganization"].ToString() : string.Empty,
                TotalExperienceYears = row.Table.Columns.Contains("TotalExperienceYears") && row["TotalExperienceYears"] != DBNull.Value ? (int?)Convert.ToInt32(row["TotalExperienceYears"]) : null,
                PastTeachingExperience = row.Table.Columns.Contains("PastTeachingExperience") && row["PastTeachingExperience"] != DBNull.Value ? row["PastTeachingExperience"].ToString() : string.Empty,
                PrimaryExpertise = row.Table.Columns.Contains("PrimaryExpertise") && row["PrimaryExpertise"] != DBNull.Value ? row["PrimaryExpertise"].ToString() : string.Empty,
                Bio = row.Table.Columns.Contains("Bio") && row["Bio"] != DBNull.Value ? row["Bio"].ToString() : string.Empty,
                HighestDegree = row.Table.Columns.Contains("HighestDegree") && row["HighestDegree"] != DBNull.Value ? row["HighestDegree"].ToString() : string.Empty,
                InstitutionName = row.Table.Columns.Contains("InstitutionName") && row["InstitutionName"] != DBNull.Value ? row["InstitutionName"].ToString() : string.Empty,
                GraduationYear = row.Table.Columns.Contains("GraduationYear") && row["GraduationYear"] != DBNull.Value ? (int?)Convert.ToInt32(row["GraduationYear"]) : null,
                PlannedCourses = row.Table.Columns.Contains("PlannedCourses") && row["PlannedCourses"] != DBNull.Value ? row["PlannedCourses"].ToString() : string.Empty,
                TargetLevel = row.Table.Columns.Contains("TargetLevel") && row["TargetLevel"] != DBNull.Value ? row["TargetLevel"].ToString() : "Beginner",
                ProposedPricing = row.Table.Columns.Contains("ProposedPricing") && row["ProposedPricing"] != DBNull.Value ? (decimal?)Convert.ToDecimal(row["ProposedPricing"]) : 49.99m,
                ProfilePictureUrl = row.Table.Columns.Contains("ProfilePictureUrl") && row["ProfilePictureUrl"] != DBNull.Value ? row["ProfilePictureUrl"].ToString() : string.Empty,
                DigitalSignatureUrl = row.Table.Columns.Contains("DigitalSignatureUrl") && row["DigitalSignatureUrl"] != DBNull.Value ? row["DigitalSignatureUrl"].ToString() : string.Empty,
                LinkedInUrl = row.Table.Columns.Contains("LinkedInUrl") && row["LinkedInUrl"] != DBNull.Value ? row["LinkedInUrl"].ToString() : string.Empty,
                GitHubOrPortfolioUrl = row.Table.Columns.Contains("GitHubOrPortfolioUrl") && row["GitHubOrPortfolioUrl"] != DBNull.Value ? row["GitHubOrPortfolioUrl"].ToString() : string.Empty,
                CompletionPercentage = row.Table.Columns.Contains("CompletionPercentage") && row["CompletionPercentage"] != DBNull.Value ? Convert.ToInt32(row["CompletionPercentage"]) : 0,
                UpdatedAt = row.Table.Columns.Contains("UpdatedAt") && row["UpdatedAt"] != DBNull.Value ? Convert.ToDateTime(row["UpdatedAt"]) : DateTime.Now,
                AdminFeedback = row.Table.Columns.Contains("AdminFeedback") && row["AdminFeedback"] != DBNull.Value ? row["AdminFeedback"].ToString() : (row.Table.Columns.Contains("UserAdminFeedback") && row["UserAdminFeedback"] != DBNull.Value ? row["UserAdminFeedback"].ToString() : string.Empty),
                ReviewedAt = row.Table.Columns.Contains("ReviewedAt") && row["ReviewedAt"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["ReviewedAt"]) : null,
                ReviewedBy = row.Table.Columns.Contains("ReviewedBy") && row["ReviewedBy"] != DBNull.Value ? (int?)Convert.ToInt32(row["ReviewedBy"]) : null,
                ReviewerName = row.Table.Columns.Contains("ReviewerName") && row["ReviewerName"] != DBNull.Value ? row["ReviewerName"].ToString() : string.Empty,
                RegisteredAt = row.Table.Columns.Contains("RegisteredAt") && row["RegisteredAt"] != DBNull.Value ? Convert.ToDateTime(row["RegisteredAt"]) : (row.Table.Columns.Contains("CreatedAt") && row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.Now),
                PublishedCoursesCount = row.Table.Columns.Contains("PublishedCoursesCount") && row["PublishedCoursesCount"] != DBNull.Value ? Convert.ToInt32(row["PublishedCoursesCount"]) : 0
            };
        }

        private CourseModule MapRowToModule(DataRow row)
        {
            return new CourseModule
            {
                ModuleId = Convert.ToInt32(row["ModuleId"]),
                CourseId = Convert.ToInt32(row["CourseId"]),
                ModuleNumber = Convert.ToInt32(row["ModuleNumber"]),
                Title = row["Title"].ToString(),
                Description = row["Description"] != DBNull.Value ? row["Description"].ToString() : string.Empty,
                DurationMinutes = row["DurationMinutes"] != DBNull.Value ? Convert.ToInt32(row["DurationMinutes"]) : 30,
                ContentMarkdown = row["ContentMarkdown"] != DBNull.Value ? row["ContentMarkdown"].ToString() : string.Empty,
                VideoOrResourceUrl = row["VideoOrResourceUrl"] != DBNull.Value ? row["VideoOrResourceUrl"].ToString() : string.Empty,
                AssignmentPrompt = row["AssignmentPrompt"] != DBNull.Value ? row["AssignmentPrompt"].ToString() : string.Empty,
                CreatedAt = row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.Now
            };
        }

        private StudentSubmission MapRowToSubmission(DataRow row)
        {
            return new StudentSubmission
            {
                SubmissionId = Convert.ToInt32(row["SubmissionId"]),
                CourseId = Convert.ToInt32(row["CourseId"]),
                CourseTitle = row.Table.Columns.Contains("CourseTitle") && row["CourseTitle"] != DBNull.Value ? row["CourseTitle"].ToString() : string.Empty,
                ModuleId = row["ModuleId"] != DBNull.Value ? (int?)Convert.ToInt32(row["ModuleId"]) : null,
                ModuleTitle = row.Table.Columns.Contains("ModuleTitle") && row["ModuleTitle"] != DBNull.Value ? row["ModuleTitle"].ToString() : string.Empty,
                ModuleNumber = row.Table.Columns.Contains("ModuleNumber") && row["ModuleNumber"] != DBNull.Value ? (int?)Convert.ToInt32(row["ModuleNumber"]) : null,
                UserId = Convert.ToInt32(row["UserId"]),
                StudentName = row.Table.Columns.Contains("StudentName") && row["StudentName"] != DBNull.Value ? row["StudentName"].ToString() : string.Empty,
                StudentEmail = row.Table.Columns.Contains("StudentEmail") && row["StudentEmail"] != DBNull.Value ? row["StudentEmail"].ToString() : string.Empty,
                SubmissionTitle = row["SubmissionTitle"] != DBNull.Value ? row["SubmissionTitle"].ToString() : string.Empty,
                SubmissionText = row["SubmissionText"] != DBNull.Value ? row["SubmissionText"].ToString() : string.Empty,
                SubmissionFileUrl = row["SubmissionFileUrl"] != DBNull.Value ? row["SubmissionFileUrl"].ToString() : string.Empty,
                SubmittedAt = Convert.ToDateTime(row["SubmittedAt"]),
                MarksObtained = row["MarksObtained"] != DBNull.Value ? (decimal?)Convert.ToDecimal(row["MarksObtained"]) : null,
                MaxMarks = row.Table.Columns.Contains("MaxMarks") && row["MaxMarks"] != DBNull.Value ? Convert.ToDecimal(row["MaxMarks"]) : 100.00m,
                InstructorFeedback = row["InstructorFeedback"] != DBNull.Value ? row["InstructorFeedback"].ToString() : string.Empty,
                Status = row["Status"] != DBNull.Value ? row["Status"].ToString() : "Pending Review",
                ReviewedAt = row["ReviewedAt"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["ReviewedAt"]) : null,
                InstructorName = row.Table.Columns.Contains("InstructorName") && row["InstructorName"] != DBNull.Value ? row["InstructorName"].ToString() : string.Empty
            };
        }
    }
}
