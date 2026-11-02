using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public class QuizRepository : IQuizRepository
    {
        public IEnumerable<QuizQuestion> GetQuizByCourseAndLesson(int courseId, int lessonNumber)
        {
            DatabaseInitializer.Initialize();
            var list = new List<QuizQuestion>();

            try
            {
                string sql = "SELECT * FROM LMS.QuizQuestions WHERE CourseId = @CourseId AND LessonNumber = @LessonNumber ORDER BY QuestionId ASC";
                var p = new[]
                {
                    new SqlParameter("@CourseId", courseId),
                    new SqlParameter("@LessonNumber", lessonNumber)
                };

                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapRowToQuestion(row));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetQuizByCourseAndLesson error: " + ex.Message);
            }

            return list;
        }

        public IEnumerable<QuizQuestion> GetAllQuestionsForCourse(int courseId)
        {
            DatabaseInitializer.Initialize();
            var list = new List<QuizQuestion>();

            try
            {
                string sql = "SELECT * FROM LMS.QuizQuestions WHERE CourseId = @CourseId ORDER BY LessonNumber ASC, QuestionId ASC";
                var p = new[] { new SqlParameter("@CourseId", courseId) };

                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapRowToQuestion(row));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetAllQuestionsForCourse error: " + ex.Message);
            }

            return list;
        }

        public QuizQuestion GetQuestionById(int questionId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                string sql = "SELECT TOP 1 * FROM LMS.QuizQuestions WHERE QuestionId = @QuestionId";
                var p = new[] { new SqlParameter("@QuestionId", questionId) };

                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapRowToQuestion(dt.Rows[0]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetQuestionById error: " + ex.Message);
            }

            return null;
        }

        public OperationResult CreateQuestion(QuizQuestion q)
        {
            if (q == null) throw new ArgumentNullException(nameof(q));
            DatabaseInitializer.Initialize();

            try
            {
                string insertSql = @"
                    INSERT INTO LMS.QuizQuestions (CourseId, LessonNumber, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectOption)
                    VALUES (@CourseId, @LessonNumber, @QuestionText, @OptionA, @OptionB, @OptionC, @OptionD, @CorrectOption);
                    SELECT SCOPE_IDENTITY();";

                var p = new[]
                {
                    new SqlParameter("@CourseId", q.CourseId),
                    new SqlParameter("@LessonNumber", q.LessonNumber),
                    new SqlParameter("@QuestionText", (object)q.QuestionText ?? DBNull.Value),
                    new SqlParameter("@OptionA", (object)q.OptionA ?? DBNull.Value),
                    new SqlParameter("@OptionB", (object)q.OptionB ?? DBNull.Value),
                    new SqlParameter("@OptionC", (object)q.OptionC ?? DBNull.Value),
                    new SqlParameter("@OptionD", (object)q.OptionD ?? DBNull.Value),
                    new SqlParameter("@CorrectOption", (object)q.CorrectOption?.Trim().ToUpper() ?? "A")
                };

                object scalar = DbHelper.ExecuteScalar(insertSql, p);
                if (scalar != null && scalar != DBNull.Value && decimal.TryParse(scalar.ToString(), out decimal newId))
                {
                    return OperationResult.Success("Quiz question created successfully.", Convert.ToInt32(newId));
                }

                return OperationResult.Success("Quiz question created successfully.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to create quiz question: " + ex.Message);
            }
        }

        public OperationResult UpdateQuestion(QuizQuestion q)
        {
            if (q == null) throw new ArgumentNullException(nameof(q));
            DatabaseInitializer.Initialize();

            try
            {
                string updateSql = @"
                    UPDATE LMS.QuizQuestions
                    SET QuestionText = @QuestionText,
                        OptionA = @OptionA,
                        OptionB = @OptionB,
                        OptionC = @OptionC,
                        OptionD = @OptionD,
                        CorrectOption = @CorrectOption
                    WHERE QuestionId = @QuestionId;";

                var p = new[]
                {
                    new SqlParameter("@QuestionId", q.QuestionId),
                    new SqlParameter("@QuestionText", (object)q.QuestionText ?? DBNull.Value),
                    new SqlParameter("@OptionA", (object)q.OptionA ?? DBNull.Value),
                    new SqlParameter("@OptionB", (object)q.OptionB ?? DBNull.Value),
                    new SqlParameter("@OptionC", (object)q.OptionC ?? DBNull.Value),
                    new SqlParameter("@OptionD", (object)q.OptionD ?? DBNull.Value),
                    new SqlParameter("@CorrectOption", (object)q.CorrectOption?.Trim().ToUpper() ?? "A")
                };

                int rows = DbHelper.ExecuteNonQuery(updateSql, p);
                return OperationResult.Success("Quiz question updated successfully.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to update quiz question: " + ex.Message);
            }
        }

        public OperationResult DeleteQuestion(int questionId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                string deleteSql = "DELETE FROM LMS.QuizQuestions WHERE QuestionId = @QuestionId";
                var p = new[] { new SqlParameter("@QuestionId", questionId) };

                DbHelper.ExecuteNonQuery(deleteSql, p);
                return OperationResult.Success("Quiz question deleted successfully.");
            }
            catch (Exception ex)
            {
                return OperationResult.Failure("Failed to delete quiz question: " + ex.Message);
            }
        }

        private QuizQuestion MapRowToQuestion(DataRow row)
        {
            return new QuizQuestion
            {
                QuestionId = Convert.ToInt32(row["QuestionId"]),
                CourseId = Convert.ToInt32(row["CourseId"]),
                LessonNumber = Convert.ToInt32(row["LessonNumber"]),
                QuestionText = row["QuestionText"].ToString(),
                OptionA = row["OptionA"].ToString(),
                OptionB = row["OptionB"].ToString(),
                OptionC = row["OptionC"].ToString(),
                OptionD = row["OptionD"].ToString(),
                CorrectOption = row["CorrectOption"].ToString(),
                CreatedAt = row.Table.Columns.Contains("CreatedAt") && row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.Now
            };
        }
    }
}
