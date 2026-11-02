using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Repositories
{
    public class CourseRepository : ICourseRepository
    {
        public IEnumerable<Course> GetAllCourses(string searchTerm = null, string level = null)
        {
            DatabaseInitializer.Initialize();
            var courses = new List<Course>();

            try
            {
                string sql = @"
                    SELECT c.CourseId, c.Title, c.Description, c.InstructorId, 
                           ISNULL(u.FullName, c.InstructorName) AS InstructorName, 
                           c.DurationWeeks, c.Level, c.Category, c.Price, c.CreatedAt,
                           (SELECT COUNT(1) FROM LMS.Enrollments e WHERE e.CourseId = c.CourseId) AS TotalEnrolled
                    FROM LMS.Courses c
                    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                    WHERE (@SearchTerm IS NULL OR c.Title LIKE '%' + @SearchTerm + '%' OR c.Description LIKE '%' + @SearchTerm + '%')
                      AND (@Level IS NULL OR @Level = 'All' OR c.Level = @Level)
                    ORDER BY c.CreatedAt DESC";

                var p = new[]
                {
                    new SqlParameter("@SearchTerm", string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : searchTerm.Trim()),
                    new SqlParameter("@Level", string.IsNullOrWhiteSpace(level) || level == "All" ? (object)DBNull.Value : level.Trim())
                };

                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        courses.Add(MapDataRowToCourse(row));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetAllCourses error: " + ex.Message);
            }

            return courses;
        }

        public IEnumerable<CategoryFilterItem> GetCourseCategories()
        {
            DatabaseInitializer.Initialize();
            var list = new List<CategoryFilterItem>();

            try
            {
                string sql = @"
                    SELECT 
                        LTRIM(RTRIM(Category)) AS CategoryName, 
                        COUNT(*) AS CourseCount
                    FROM LMS.Courses
                    WHERE Category IS NOT NULL AND LTRIM(RTRIM(Category)) <> ''
                    GROUP BY LTRIM(RTRIM(Category))
                    ORDER BY COUNT(*) DESC, LTRIM(RTRIM(Category)) ASC;";

                DataTable dt = DbHelper.ExecuteDataTable(sql);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(new CategoryFilterItem
                        {
                            CategoryName = row["CategoryName"].ToString(),
                            CourseCount = Convert.ToInt32(row["CourseCount"])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetCourseCategories error: " + ex.Message);
            }

            return list;
        }

        public IEnumerable<Course> GetCoursesFiltered(string searchTerm = null, IEnumerable<string> categories = null, string level = null, string priceFilter = null, string sortBy = null)
        {
            DatabaseInitializer.Initialize();
            var courses = new List<Course>();

            try
            {
                var queryBuilder = new StringBuilder(@"
                    SELECT c.CourseId, c.Title, c.Description, c.InstructorId, 
                           ISNULL(u.FullName, c.InstructorName) AS InstructorName, 
                           c.DurationWeeks, c.Level, c.Category, c.Price, c.CreatedAt,
                           (SELECT COUNT(1) FROM LMS.Enrollments e WHERE e.CourseId = c.CourseId) AS TotalEnrolled
                    FROM LMS.Courses c
                    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                    WHERE 1 = 1 ");

                var parameters = new List<SqlParameter>();

                // Keyword search
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    queryBuilder.Append(" AND (c.Title LIKE @SearchTerm OR c.Description LIKE @SearchTerm OR ISNULL(u.FullName, c.InstructorName) LIKE @SearchTerm OR c.Category LIKE @SearchTerm) ");
                    parameters.Add(new SqlParameter("@SearchTerm", "%" + searchTerm.Trim() + "%"));
                }

                // Level filter
                if (!string.IsNullOrWhiteSpace(level) && !level.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    queryBuilder.Append(" AND c.Level = @Level ");
                    parameters.Add(new SqlParameter("@Level", level.Trim()));
                }

                // Multi-category filter
                if (categories != null)
                {
                    var catList = categories.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    if (catList.Count > 0 && !catList.Any(c => c.Equals("All", StringComparison.OrdinalIgnoreCase)))
                    {
                        var paramNames = new List<string>();
                        for (int i = 0; i < catList.Count; i++)
                        {
                            string pName = "@Cat_" + i;
                            paramNames.Add(pName);
                            parameters.Add(new SqlParameter(pName, catList[i]));
                        }
                        queryBuilder.Append($" AND LTRIM(RTRIM(c.Category)) IN ({string.Join(", ", paramNames)}) ");
                    }
                }

                // Pricing filter
                if (!string.IsNullOrWhiteSpace(priceFilter) && !priceFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    if (priceFilter.Equals("Free", StringComparison.OrdinalIgnoreCase))
                    {
                        queryBuilder.Append(" AND c.Price <= 0 ");
                    }
                    else if (priceFilter.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                    {
                        queryBuilder.Append(" AND c.Price > 0 ");
                    }
                    else if (priceFilter.Equals("Under50", StringComparison.OrdinalIgnoreCase))
                    {
                        queryBuilder.Append(" AND c.Price > 0 AND c.Price <= 50.00 ");
                    }
                    else if (priceFilter.Equals("Over50", StringComparison.OrdinalIgnoreCase))
                    {
                        queryBuilder.Append(" AND c.Price > 50.00 ");
                    }
                }

                // Sorting
                switch ((sortBy ?? "").ToLowerInvariant())
                {
                    case "newest":
                        queryBuilder.Append(" ORDER BY c.CreatedAt DESC, c.CourseId DESC ");
                        break;
                    case "priceasc":
                        queryBuilder.Append(" ORDER BY c.Price ASC, c.CourseId ASC ");
                        break;
                    case "pricedesc":
                        queryBuilder.Append(" ORDER BY c.Price DESC, c.CourseId ASC ");
                        break;
                    case "titleasc":
                        queryBuilder.Append(" ORDER BY c.Title ASC ");
                        break;
                    case "popular":
                    default:
                        queryBuilder.Append(" ORDER BY TotalEnrolled DESC, c.CreatedAt DESC ");
                        break;
                }

                DataTable dt = DbHelper.ExecuteDataTable(queryBuilder.ToString(), parameters.ToArray());
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        courses.Add(MapDataRowToCourse(row));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetCoursesFiltered error: " + ex.Message);
            }

            return courses;
        }

        public IEnumerable<Course> GetCoursesByInstructor(int instructorId)
        {
            DatabaseInitializer.Initialize();
            var allCourses = GetAllCourses();
            return allCourses.Where(c => c.InstructorId == instructorId).ToList();
        }

        public Course GetCourseById(int courseId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                string sql = @"
                    SELECT TOP 1 c.CourseId, c.Title, c.Description, c.InstructorId, 
                           ISNULL(u.FullName, c.InstructorName) AS InstructorName, 
                           c.DurationWeeks, c.Level, c.Category, c.Price, c.CreatedAt,
                           (SELECT COUNT(1) FROM LMS.Enrollments e WHERE e.CourseId = c.CourseId) AS TotalEnrolled
                    FROM LMS.Courses c
                    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                    WHERE c.CourseId = @CourseId";

                var p = new[] { new SqlParameter("@CourseId", courseId) };
                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapDataRowToCourse(dt.Rows[0]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetCourseById error: " + ex.Message);
            }

            return null;
        }

        public Course CreateCourse(Course course)
        {
            if (course == null) throw new ArgumentNullException(nameof(course));
            DatabaseInitializer.Initialize();

            try
            {
                string sql = @"
                    INSERT INTO LMS.Courses (Title, Description, InstructorId, InstructorName, DurationWeeks, Level, Category, Price, CreatedAt)
                    VALUES (@Title, @Description, @InstructorId, @InstructorName, @DurationWeeks, @Level, @Category, @Price, GETDATE());
                    SELECT SCOPE_IDENTITY();";

                var p = new[]
                {
                    new SqlParameter("@Title", (object)course.Title ?? DBNull.Value),
                    new SqlParameter("@Description", (object)course.Description ?? DBNull.Value),
                    new SqlParameter("@InstructorId", course.InstructorId.HasValue ? (object)course.InstructorId.Value : DBNull.Value),
                    new SqlParameter("@InstructorName", (object)course.InstructorName ?? DBNull.Value),
                    new SqlParameter("@DurationWeeks", course.DurationWeeks),
                    new SqlParameter("@Level", (object)course.Level ?? "Beginner"),
                    new SqlParameter("@Category", (object)course.Category ?? "Frontend"),
                    new SqlParameter("@Price", course.Price)
                };

                object scalar = DbHelper.ExecuteScalar(sql, p);
                if (scalar != null && scalar != DBNull.Value && decimal.TryParse(scalar.ToString(), out decimal newIdDec))
                {
                    int newId = Convert.ToInt32(newIdDec);
                    return GetCourseById(newId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CreateCourse error: " + ex.Message);
            }

            return null;
        }

        public bool UpdateCourse(Course course)
        {
            if (course == null) throw new ArgumentNullException(nameof(course));
            DatabaseInitializer.Initialize();

            try
            {
                string sql = @"
                    UPDATE LMS.Courses 
                    SET Title = @Title, 
                        Description = @Description, 
                        DurationWeeks = @DurationWeeks, 
                        Level = @Level, 
                        Category = @Category, 
                        Price = @Price
                    WHERE CourseId = @CourseId";

                var p = new[]
                {
                    new SqlParameter("@CourseId", course.CourseId),
                    new SqlParameter("@Title", (object)course.Title ?? DBNull.Value),
                    new SqlParameter("@Description", (object)course.Description ?? DBNull.Value),
                    new SqlParameter("@DurationWeeks", course.DurationWeeks),
                    new SqlParameter("@Level", (object)course.Level ?? "Beginner"),
                    new SqlParameter("@Category", (object)course.Category ?? "Frontend"),
                    new SqlParameter("@Price", course.Price)
                };

                int rows = DbHelper.ExecuteNonQuery(sql, p);
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateCourse error: " + ex.Message);
                return false;
            }
        }

        public bool DeleteCourse(int courseId)
        {
            DatabaseInitializer.Initialize();

            try
            {
                string sql = @"
                    DELETE FROM LMS.QuizQuestions WHERE CourseId = @CourseId;
                    DELETE FROM LMS.Enrollments WHERE CourseId = @CourseId;
                    DELETE FROM LMS.Reviews WHERE CourseId = @CourseId;
                    DELETE FROM LMS.Payments WHERE CourseId = @CourseId;
                    DELETE FROM LMS.Courses WHERE CourseId = @CourseId;";

                var p = new[] { new SqlParameter("@CourseId", courseId) };
                int rows = DbHelper.ExecuteNonQuery(sql, p);
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DeleteCourse error: " + ex.Message);
                return false;
            }
        }

        private Course MapDataRowToCourse(DataRow row)
        {
            return new Course
            {
                CourseId = Convert.ToInt32(row["CourseId"]),
                Title = row["Title"].ToString(),
                InstructorId = row.Table.Columns.Contains("InstructorId") && row["InstructorId"] != DBNull.Value ? (int?)Convert.ToInt32(row["InstructorId"]) : null,
                InstructorName = row.Table.Columns.Contains("InstructorName") && row["InstructorName"] != DBNull.Value ? row["InstructorName"].ToString() : string.Empty,
                Category = row.Table.Columns.Contains("Category") && row["Category"] != DBNull.Value ? row["Category"].ToString() : "Frontend",
                Price = row.Table.Columns.Contains("Price") && row["Price"] != DBNull.Value ? Convert.ToDecimal(row["Price"]) : 49.99m,
                DurationWeeks = row.Table.Columns.Contains("DurationWeeks") && row["DurationWeeks"] != DBNull.Value ? Convert.ToInt32(row["DurationWeeks"]) : 6,
                Level = row.Table.Columns.Contains("Level") && row["Level"] != DBNull.Value ? row["Level"].ToString() : "Beginner",
                Description = row.Table.Columns.Contains("Description") && row["Description"] != DBNull.Value ? row["Description"].ToString() : string.Empty,
                CreatedAt = row.Table.Columns.Contains("CreatedAt") && row["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(row["CreatedAt"]) : DateTime.Now,
                TotalEnrolled = row.Table.Columns.Contains("TotalEnrolled") && row["TotalEnrolled"] != DBNull.Value ? Convert.ToInt32(row["TotalEnrolled"]) : 0
            };
        }
    }
}
