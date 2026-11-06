using WebApplication1.Services;
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
            bool isDefaultQuery = string.IsNullOrWhiteSpace(searchTerm) && (string.IsNullOrWhiteSpace(level) || level == "All");
            if (isDefaultQuery)
            {
                var cached = RedisCacheService.Get<List<Course>>("catalog:courses:all");
                if (cached != null && cached.Any())
                {
                    return cached;
                }
            }

            DatabaseInitializer.Initialize();
            var courses = new List<Course>();

            try
            {
                string sql = @"
                    SELECT c.CourseId, c.Title, c.Description, c.InstructorId, 
                           ISNULL(u.FullName, c.InstructorName) AS InstructorName, 
                           c.DurationWeeks, c.Level, c.Category, c.Price, c.CreatedAt,
                           c.IsPublished, c.Status,
                           (SELECT COUNT(1) FROM LMS.Enrollments e WHERE e.CourseId = c.CourseId) AS TotalEnrolled,
                           (SELECT COUNT(1) FROM LMS.CourseModules m WHERE m.CourseId = c.CourseId) AS ModuleCount
                    FROM LMS.Courses c
                    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                    WHERE (@SearchTerm IS NULL OR c.Title LIKE '%' + @SearchTerm + '%' OR c.Description LIKE '%' + @SearchTerm + '%')
                      AND (@Level IS NULL OR @Level = 'All' OR c.Level = @Level)
                      AND (c.IsPublished = 1 OR c.Status = 'Published')
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

            if (isDefaultQuery && courses.Any())
            {
                RedisCacheService.Set("catalog:courses:all", courses, TimeSpan.FromMinutes(15));
            }

            return courses;
        }

        public IEnumerable<CategoryFilterItem> GetCourseCategories()
        {
            var cached = RedisCacheService.Get<List<CategoryFilterItem>>("catalog:categories");
            if (cached != null && cached.Any())
            {
                return cached;
            }

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
                      AND (IsPublished = 1 OR Status = 'Published')
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

            if (list.Any())
            {
                RedisCacheService.Set("catalog:categories", list, TimeSpan.FromMinutes(30));
            }

            return list;
        }

        public IEnumerable<Course> GetCoursesFiltered(string searchTerm = null, IEnumerable<string> categories = null, string level = null, string priceFilter = null, string sortBy = null)
        {
            bool isDefault = string.IsNullOrWhiteSpace(searchTerm)
                && (categories == null || !categories.Any())
                && (string.IsNullOrWhiteSpace(level) || level.Equals("All", StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(priceFilter) || priceFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(sortBy) || sortBy.Equals("popular", StringComparison.OrdinalIgnoreCase) || sortBy.Equals("newest", StringComparison.OrdinalIgnoreCase));

            if (isDefault)
            {
                var cached = RedisCacheService.Get<List<Course>>("catalog:courses:filtered:default");
                if (cached != null && cached.Any())
                {
                    return cached;
                }
            }

            DatabaseInitializer.Initialize();
            var courses = new List<Course>();

            try
            {
                var queryBuilder = new StringBuilder(@"
                    SELECT c.CourseId, c.Title, c.Description, c.InstructorId, 
                           ISNULL(u.FullName, c.InstructorName) AS InstructorName, 
                           c.DurationWeeks, c.Level, c.Category, c.Price, c.CreatedAt,
                           c.IsPublished, c.Status,
                           (SELECT COUNT(1) FROM LMS.Enrollments e WHERE e.CourseId = c.CourseId) AS TotalEnrolled,
                           (SELECT COUNT(1) FROM LMS.CourseModules m WHERE m.CourseId = c.CourseId) AS ModuleCount
                    FROM LMS.Courses c
                    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                    WHERE (c.IsPublished = 1 OR c.Status = 'Published') ");

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

            if (isDefault && courses.Any())
            {
                RedisCacheService.Set("catalog:courses:filtered:default", courses, TimeSpan.FromMinutes(15));
            }

            return courses;
        }

        public IEnumerable<Course> GetCoursesByInstructor(int instructorId)
        {
            DatabaseInitializer.Initialize();
            var courses = new List<Course>();

            try
            {
                string sql = @"
                    SELECT c.CourseId, c.Title, c.Description, c.InstructorId, 
                           ISNULL(u.FullName, c.InstructorName) AS InstructorName, 
                           c.DurationWeeks, c.Level, c.Category, c.Price, c.CreatedAt,
                           c.IsPublished, c.Status,
                           (SELECT COUNT(1) FROM LMS.Enrollments e WHERE e.CourseId = c.CourseId) AS TotalEnrolled,
                           (SELECT COUNT(1) FROM LMS.CourseModules m WHERE m.CourseId = c.CourseId) AS ModuleCount
                    FROM LMS.Courses c
                    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                    WHERE c.InstructorId = @InstructorId
                    ORDER BY c.CreatedAt DESC";

                var p = new[] { new SqlParameter("@InstructorId", instructorId) };
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
                System.Diagnostics.Debug.WriteLine("GetCoursesByInstructor error: " + ex.Message);
            }

            return courses;
        }

        public Course GetCourseById(int courseId)
        {
            string cacheKey = $"course:details:{courseId}";
            var cached = RedisCacheService.Get<Course>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            DatabaseInitializer.Initialize();

            try
            {
                string sql = @"
                    SELECT TOP 1 c.CourseId, c.Title, c.Description, c.InstructorId, 
                           ISNULL(u.FullName, c.InstructorName) AS InstructorName, 
                           c.DurationWeeks, c.Level, c.Category, c.Price, c.CreatedAt,
                           c.IsPublished, c.Status,
                           (SELECT COUNT(1) FROM LMS.Enrollments e WHERE e.CourseId = c.CourseId) AS TotalEnrolled,
                           (SELECT COUNT(1) FROM LMS.CourseModules m WHERE m.CourseId = c.CourseId) AS ModuleCount
                    FROM LMS.Courses c
                    LEFT JOIN LMS.Users u ON c.InstructorId = u.UserId
                    WHERE c.CourseId = @CourseId";

                var p = new[] { new SqlParameter("@CourseId", courseId) };
                DataTable dt = DbHelper.ExecuteDataTable(sql, p);
                if (dt != null && dt.Rows.Count > 0)
                {
                    var course = MapDataRowToCourse(dt.Rows[0]);
                    if (course != null)
                    {
                        RedisCacheService.Set(cacheKey, course, TimeSpan.FromMinutes(30));
                    }
                    return course;
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
                // New courses are created in Draft status by default
                bool initialPublished = false;
                string initialStatus = "Draft";

                string sql = @"
                    INSERT INTO LMS.Courses (Title, Description, InstructorId, InstructorName, DurationWeeks, Level, Category, Price, CreatedAt, IsPublished, Status)
                    VALUES (@Title, @Description, @InstructorId, @InstructorName, @DurationWeeks, @Level, @Category, @Price, GETDATE(), @IsPublished, @Status);
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
                    new SqlParameter("@Price", course.Price),
                    new SqlParameter("@IsPublished", initialPublished),
                    new SqlParameter("@Status", initialStatus)
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

        public bool SetCoursePublishStatus(int courseId, bool isPublished, out string errorMessage)
        {
            errorMessage = string.Empty;
            DatabaseInitializer.Initialize();

            try
            {
                if (isPublished)
                {
                    // Validation gate: course must have at least 1 module AND at least 1 module with content
                    string checkSql = @"
                        SELECT 
                            COUNT(1) AS TotalModules,
                            COUNT(CASE WHEN 
                                LEN(LTRIM(RTRIM(ISNULL(ContentMarkdown, '')))) > 0 OR
                                LEN(LTRIM(RTRIM(ISNULL(VideoOrResourceUrl, '')))) > 0 OR
                                LEN(LTRIM(RTRIM(ISNULL(AssignmentPrompt, '')))) > 0 OR
                                LEN(LTRIM(RTRIM(ISNULL(Description, '')))) > 0
                                THEN 1 END) AS ModulesWithContent
                        FROM LMS.CourseModules
                        WHERE CourseId = @CourseId";

                    var checkParams = new[] { new SqlParameter("@CourseId", courseId) };
                    DataTable dt = DbHelper.ExecuteDataTable(checkSql, checkParams);

                    int totalModules = 0;
                    int withContent = 0;
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        totalModules = Convert.ToInt32(dt.Rows[0]["TotalModules"]);
                        withContent = Convert.ToInt32(dt.Rows[0]["ModulesWithContent"]);
                    }

                    if (totalModules == 0)
                    {
                        errorMessage = "Cannot publish course: You must create at least one curriculum module before publishing to the student catalog.";
                        return false;
                    }

                    if (withContent == 0)
                    {
                        errorMessage = "Cannot publish course: At least one module must contain lesson content, study materials, or video resources.";
                        return false;
                    }

                    string updateSql = "UPDATE LMS.Courses SET IsPublished = 1, Status = 'Published' WHERE CourseId = @CourseId";
                    int rows = DbHelper.ExecuteNonQuery(updateSql, new[] { new SqlParameter("@CourseId", courseId) });
                    if (rows > 0) InvalidateCourseCache(courseId);
                    return rows > 0;
                }
                else
                {
                    string updateSql = "UPDATE LMS.Courses SET IsPublished = 0, Status = 'Draft' WHERE CourseId = @CourseId";
                    int rows = DbHelper.ExecuteNonQuery(updateSql, new[] { new SqlParameter("@CourseId", courseId) });
                    if (rows > 0) InvalidateCourseCache(courseId);
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Database error: " + ex.Message;
                return false;
            }
        }

        public bool CanCourseBePublished(int courseId, out int moduleCount, out string errorMessage)
        {
            moduleCount = 0;
            errorMessage = string.Empty;
            DatabaseInitializer.Initialize();

            try
            {
                string checkSql = @"
                    SELECT 
                        COUNT(1) AS TotalModules,
                        COUNT(CASE WHEN 
                            LEN(LTRIM(RTRIM(ISNULL(ContentMarkdown, '')))) > 0 OR
                            LEN(LTRIM(RTRIM(ISNULL(VideoOrResourceUrl, '')))) > 0 OR
                            LEN(LTRIM(RTRIM(ISNULL(AssignmentPrompt, '')))) > 0 OR
                            LEN(LTRIM(RTRIM(ISNULL(Description, '')))) > 0
                            THEN 1 END) AS ModulesWithContent
                    FROM LMS.CourseModules
                    WHERE CourseId = @CourseId";

                var checkParams = new[] { new SqlParameter("@CourseId", courseId) };
                DataTable dt = DbHelper.ExecuteDataTable(checkSql, checkParams);

                int withContent = 0;
                if (dt != null && dt.Rows.Count > 0)
                {
                    moduleCount = Convert.ToInt32(dt.Rows[0]["TotalModules"]);
                    withContent = Convert.ToInt32(dt.Rows[0]["ModulesWithContent"]);
                }

                if (moduleCount == 0)
                {
                    errorMessage = "This course has 0 modules. Add at least one module with content to enable publishing.";
                    return false;
                }

                if (withContent == 0)
                {
                    errorMessage = "This course has modules, but no lesson content or study materials have been added yet.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
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
                if (rows > 0) InvalidateCourseCache(course.CourseId);
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
                TotalEnrolled = row.Table.Columns.Contains("TotalEnrolled") && row["TotalEnrolled"] != DBNull.Value ? Convert.ToInt32(row["TotalEnrolled"]) : 0,
                IsPublished = row.Table.Columns.Contains("IsPublished") && row["IsPublished"] != DBNull.Value && Convert.ToBoolean(row["IsPublished"]),
                Status = row.Table.Columns.Contains("Status") && row["Status"] != DBNull.Value ? row["Status"].ToString() : (row.Table.Columns.Contains("IsPublished") && row["IsPublished"] != DBNull.Value && Convert.ToBoolean(row["IsPublished"]) ? "Published" : "Draft"),
                ModuleCount = row.Table.Columns.Contains("ModuleCount") && row["ModuleCount"] != DBNull.Value ? Convert.ToInt32(row["ModuleCount"]) : 0
            };
        }

        private void InvalidateCourseCache(int courseId)
        {
            try
            {
                RedisCacheService.Remove("catalog:courses:all");
                RedisCacheService.Remove("catalog:courses:filtered:default");
                RedisCacheService.Remove("catalog:categories");
                RedisCacheService.Remove($"course:details:{courseId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InvalidateCourseCache error: " + ex.Message);
            }
        }
    }
}
