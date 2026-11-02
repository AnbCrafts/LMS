using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public class ReviewRepository : IReviewRepository
    {
        public OperationResult AddReview(int courseId, int userId, int rating, string comment)
        {
            DatabaseInitializer.Initialize();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@CourseId", courseId),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@Rating", rating),
                    new SqlParameter("@Comment", comment ?? string.Empty)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_AddReview", parameters);
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
                string sql = @"
                    INSERT INTO LMS.Reviews (CourseId, UserId, Rating, Comment, CreatedAt)
                    VALUES (@CourseId, @UserId, @Rating, @Comment, GETDATE());";

                var p = new[]
                {
                    new SqlParameter("@CourseId", courseId),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@Rating", rating),
                    new SqlParameter("@Comment", comment ?? string.Empty)
                };

                int rows = DbHelper.ExecuteNonQuery(sql, p);
                return OperationResult.Success(rows > 0 ? "Review added successfully!" : "Failed to add review.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to add review: " + ex.Message);
            }
        }

        public IEnumerable<Review> GetCourseReviews(int courseId)
        {
            DatabaseInitializer.Initialize();
            var list = new List<Review>();

            try
            {
                var parameters = new[]
                {
                    new SqlParameter("@CourseId", courseId)
                };

                DataTable dt = DbHelper.ExecuteDataTable("LMS.sp_GetCourseReviews", parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new Review
                        {
                            ReviewId = Convert.ToInt32(row["ReviewId"]),
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            UserId = Convert.ToInt32(row["UserId"]),
                            ReviewerName = row["ReviewerName"].ToString(),
                            Rating = Convert.ToInt32(row["Rating"]),
                            Comment = row["Comment"] != DBNull.Value ? row["Comment"].ToString() : string.Empty,
                            CreatedAt = Convert.ToDateTime(row["CreatedAt"])
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
                    SELECT r.ReviewId, r.CourseId, r.UserId, 
                           ISNULL(u.FullName, 'Learner') AS ReviewerName,
                           r.Rating, r.Comment, r.CreatedAt
                    FROM LMS.Reviews r
                    LEFT JOIN LMS.Users u ON r.UserId = u.UserId
                    WHERE r.CourseId = @CourseId
                    ORDER BY r.CreatedAt DESC";

                var p = new[] { new SqlParameter("@CourseId", courseId) };
                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new Review
                        {
                            ReviewId = Convert.ToInt32(row["ReviewId"]),
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            UserId = Convert.ToInt32(row["UserId"]),
                            ReviewerName = row["ReviewerName"].ToString(),
                            Rating = Convert.ToInt32(row["Rating"]),
                            Comment = row["Comment"] != DBNull.Value ? row["Comment"].ToString() : string.Empty,
                            CreatedAt = Convert.ToDateTime(row["CreatedAt"])
                        });
                    }
                }
            }
            catch { }

            return list;
        }
    }
}
