using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public class EnrollmentRepository : IEnrollmentRepository
    {
        public OperationResult EnrollCourse(int userId, int courseId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_EnrollCourse", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    bool isSuccess = Convert.ToInt32(dt.Rows[0]["IsSuccess"]) == 1;
                    string message = dt.Rows[0]["Message"].ToString();
                    int? enrollmentId = dt.Columns.Contains("EnrollmentId") && dt.Rows[0]["EnrollmentId"] != DBNull.Value 
                        ? (int?)Convert.ToInt32(dt.Rows[0]["EnrollmentId"]) 
                        : null;

                    return new OperationResult
                    {
                        IsSuccess = isSuccess,
                        Message = message,
                        TargetId = enrollmentId
                    };
                }
            }
            catch
            {
                // Fallback to direct SQL
            }

            try
            {
                string sql = @"
                    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @UserId AND CourseId = @CourseId)
                    BEGIN
                        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
                        OUTPUT INSERTED.EnrollmentId
                        VALUES (@UserId, @CourseId, GETDATE(), 0, 0);
                    END
                    ELSE
                    BEGIN
                        SELECT EnrollmentId FROM LMS.Enrollments WHERE UserId = @UserId AND CourseId = @CourseId;
                    END";

                var p = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId)
                };

                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                int? eid = dt != null && dt.Rows.Count > 0 ? (int?)Convert.ToInt32(dt.Rows[0]["EnrollmentId"]) : null;

                return new OperationResult
                {
                    IsSuccess = true,
                    Message = "Successfully enrolled in course.",
                    TargetId = eid
                };
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to enroll: " + ex.Message);
            }
        }

        public IEnumerable<StudentDashboardViewModel> GetStudentDashboard(int userId)
        {
            DatabaseInitializer.Initialize();
            var list = new List<StudentDashboardViewModel>();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@UserId", userId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetStudentDashboard", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new StudentDashboardViewModel
                        {
                            EnrollmentId = Convert.ToInt32(row["EnrollmentId"]),
                            UserId = Convert.ToInt32(row["UserId"]),
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            Course = row["Course"].ToString(),
                            InstructorName = row["InstructorName"] != DBNull.Value ? row["InstructorName"].ToString() : string.Empty,
                            DurationWeeks = row.Table.Columns.Contains("DurationWeeks") && row["DurationWeeks"] != DBNull.Value ? Convert.ToInt32(row["DurationWeeks"]) : 0,
                            Level = row.Table.Columns.Contains("Level") && row["Level"] != DBNull.Value ? row["Level"].ToString() : string.Empty,
                            Progress = Convert.ToInt32(row["Progress"]),
                            Status = row["Status"].ToString(),
                            EnrolledAt = Convert.ToDateTime(row["EnrolledAt"])
                        });
                    }
                    return list;
                }
            }
            catch
            {
                // Fallback to direct SQL
            }

            try
            {
                string sql = @"
                    SELECT e.EnrollmentId, e.UserId, e.CourseId, 
                           c.Title AS Course, 
                           ISNULL(u.FullName, c.InstructorName) AS InstructorName,
                           c.DurationWeeks, c.Level, e.ProgressPercentage AS Progress,
                           CASE WHEN e.IsCompleted = 1 OR e.ProgressPercentage >= 100 THEN 'Completed' ELSE 'In Progress' END AS Status,
                           e.EnrolledAt
                    FROM LMS.Enrollments e
                    JOIN LMS.Courses c ON e.CourseId = c.CourseId
                    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                    WHERE e.UserId = @UserId
                    ORDER BY e.EnrolledAt DESC";

                var p = new[] { new SqlParameter("@UserId", userId) };
                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new StudentDashboardViewModel
                        {
                            EnrollmentId = Convert.ToInt32(row["EnrollmentId"]),
                            UserId = Convert.ToInt32(row["UserId"]),
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            Course = row["Course"].ToString(),
                            InstructorName = row["InstructorName"] != DBNull.Value ? row["InstructorName"].ToString() : "EduPulse Instructor",
                            DurationWeeks = row.Table.Columns.Contains("DurationWeeks") && row["DurationWeeks"] != DBNull.Value ? Convert.ToInt32(row["DurationWeeks"]) : 4,
                            Level = row.Table.Columns.Contains("Level") && row["Level"] != DBNull.Value ? row["Level"].ToString() : "Beginner",
                            Progress = Convert.ToInt32(row["Progress"]),
                            Status = row["Status"].ToString(),
                            EnrolledAt = Convert.ToDateTime(row["EnrolledAt"])
                        });
                    }
                }
            }
            catch { }

            return list;
        }

        public OperationResult UpdateCourseProgress(int userId, int courseId, int progressPercentage)
        {
            DatabaseInitializer.Initialize();

            try
            {
                string sql = @"
                    IF NOT EXISTS (SELECT 1 FROM LMS.Enrollments WHERE UserId = @UserId AND CourseId = @CourseId)
                    BEGIN
                        INSERT INTO LMS.Enrollments (UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted)
                        VALUES (@UserId, @CourseId, GETDATE(), @ProgressPercentage, CASE WHEN @ProgressPercentage >= 100 THEN 1 ELSE 0 END);
                    END
                    ELSE
                    BEGIN
                        UPDATE LMS.Enrollments
                        SET ProgressPercentage = @ProgressPercentage,
                            IsCompleted = CASE WHEN @ProgressPercentage >= 100 THEN 1 ELSE 0 END
                        WHERE UserId = @UserId AND CourseId = @CourseId;
                    END";

                var p = new[]
                {
                    new SqlParameter("@ProgressPercentage", progressPercentage),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId)
                };

                DbHelper.ExecuteNonQuery(sql, p);
                return OperationResult.Success("Progress updated successfully.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to update progress: " + ex.Message);
            }
        }

        public EnrollmentStatusResult CheckEnrollment(int userId, int courseId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_CheckEnrollment", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    bool isEnrolled = Convert.ToInt32(row["IsEnrolled"]) == 1;

                    return new EnrollmentStatusResult
                    {
                        IsEnrolled = isEnrolled,
                        EnrollmentId = row["EnrollmentId"] != DBNull.Value ? (int?)Convert.ToInt32(row["EnrollmentId"]) : null,
                        ProgressPercentage = row["ProgressPercentage"] != DBNull.Value ? Convert.ToInt32(row["ProgressPercentage"]) : 0,
                        Status = row["Status"] != DBNull.Value ? row["Status"].ToString() : null,
                        EnrolledAt = row["EnrolledAt"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["EnrolledAt"]) : null
                    };
                }
            }
            catch
            {
                // Fallback to direct SQL
            }

            try
            {
                string sql = @"
                    SELECT TOP 1 EnrollmentId, UserId, CourseId, EnrolledAt, ProgressPercentage, IsCompleted,
                           CASE WHEN IsCompleted = 1 OR ProgressPercentage >= 100 THEN 'Completed' ELSE 'In Progress' END AS Status
                    FROM LMS.Enrollments
                    WHERE UserId = @UserId AND CourseId = @CourseId";

                var p = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId)
                };

                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    return new EnrollmentStatusResult
                    {
                        IsEnrolled = true,
                        EnrollmentId = Convert.ToInt32(row["EnrollmentId"]),
                        ProgressPercentage = Convert.ToInt32(row["ProgressPercentage"]),
                        Status = row["Status"].ToString(),
                        EnrolledAt = Convert.ToDateTime(row["EnrolledAt"])
                    };
                }
            }
            catch { }

            return new EnrollmentStatusResult { IsEnrolled = false };
        }

        public OperationResult UnenrollCourse(int userId, int courseId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_UnenrollCourse", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    bool isSuccess = Convert.ToInt32(dt.Rows[0]["IsSuccess"]) == 1;
                    string message = dt.Rows[0]["Message"].ToString();
                    return new OperationResult { IsSuccess = isSuccess, Message = message };
                }
            }
            catch
            {
                // Fallback to direct SQL
            }

            try
            {
                string sql = "DELETE FROM LMS.Enrollments WHERE UserId = @UserId AND CourseId = @CourseId";
                var p = new[]
                {
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@CourseId", courseId)
                };
                DbHelper.ExecuteNonQuery(sql, p);
                return OperationResult.Success("Course unenrolled successfully.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to unenroll: " + ex.Message);
            }
        }
    }
}
