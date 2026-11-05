using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebApplication1.Data;

namespace WebApplication1.Models
{
    public class InstructorNavbarViewModel
    {
        public bool IsLoggedIn { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string AvatarInitials { get; set; }
        public bool IsApproved { get; set; }
        public string Headline { get; set; }
        public string Organization { get; set; }

        // Courses for Course Switcher & Management
        public List<InstructorNavbarCourseItem> Courses { get; set; } = new List<InstructorNavbarCourseItem>();

        // Productivity Summary
        public int PendingSubmissionsCount { get; set; }
        public int TotalSubmissionsCount { get; set; }
        public int TotalStudentsCount { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal NetEarnings { get; set; }

        // Contextual Active Course info
        public int? CurrentCourseId { get; set; }
        public string CurrentCourseTitle { get; set; }
        public bool HasCurrentCourse => CurrentCourseId.HasValue && CurrentCourseId.Value > 0;

        // Structured Notifications
        public int UnreadNotificationsCount { get; set; }
        public List<InstructorNavbarNotificationItem> Notifications { get; set; } = new List<InstructorNavbarNotificationItem>();

        // Global Workspace Search Catalog
        public List<InstructorSearchItem> SearchItems { get; set; } = new List<InstructorSearchItem>();

        public static InstructorNavbarViewModel LoadForUser(User user, int? currentCourseId = null)
        {
            var vm = new InstructorNavbarViewModel();
            if (user == null && System.Web.HttpContext.Current != null && System.Web.HttpContext.Current.Session != null && System.Web.HttpContext.Current.Session["UserId"] != null)
            {
                if (int.TryParse(System.Web.HttpContext.Current.Session["UserId"].ToString(), out int sUid))
                {
                    try
                    {
                        user = new Repositories.UserRepository().GetUserById(sUid);
                    }
                    catch { }
                }
            }

            if (user == null)
            {
                vm.IsLoggedIn = false;
                return vm;
            }

            vm.IsLoggedIn = true;
            vm.UserId = user.UserId;
            vm.FullName = user.FullName ?? "Instructor";
            vm.Email = user.Email ?? "";
            vm.Role = user.Role ?? "Instructor";
            vm.IsApproved = user.IsApproved;
            vm.CurrentCourseId = currentCourseId;

            // Monogram Avatar
            if (!string.IsNullOrWhiteSpace(vm.FullName))
            {
                var parts = vm.FullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                vm.AvatarInitials = parts.Length >= 2
                    ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
                    : parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            }
            else
            {
                vm.AvatarInitials = "IN";
            }

            try
            {
                DatabaseInitializer.Initialize();

                // 1. Fetch Profile info
                string profileSql = "SELECT Headline, CurrentOrganization FROM LMS.InstructorProfiles WHERE UserId = @UserId";
                DataTable dtProf = DbHelper.ExecuteDataTable(profileSql, new[] { new SqlParameter("@UserId", user.UserId) });
                if (dtProf != null && dtProf.Rows.Count > 0)
                {
                    vm.Headline = dtProf.Rows[0]["Headline"] != DBNull.Value ? dtProf.Rows[0]["Headline"].ToString() : "";
                    vm.Organization = dtProf.Rows[0]["CurrentOrganization"] != DBNull.Value ? dtProf.Rows[0]["CurrentOrganization"].ToString() : "";
                }

                // 2. Fetch Courses
                string courseSql = @"
                    SELECT c.CourseId, c.Title, c.Category, c.Price,
                           (SELECT COUNT(*) FROM LMS.Enrollments e WHERE e.CourseId = c.CourseId) AS EnrolledCount,
                           (SELECT COUNT(*) FROM LMS.CourseModules m WHERE m.CourseId = c.CourseId) AS ModuleCount
                    FROM LMS.Courses c
                    WHERE c.InstructorId = @UserId
                    ORDER BY c.CourseId ASC;";

                DataTable dtCourses = DbHelper.ExecuteDataTable(courseSql, new[] { new SqlParameter("@UserId", user.UserId) });
                if (dtCourses != null)
                {
                    foreach (DataRow row in dtCourses.Rows)
                    {
                        var cItem = new InstructorNavbarCourseItem
                        {
                            CourseId = Convert.ToInt32(row["CourseId"]),
                            Title = row["Title"].ToString(),
                            Category = row["Category"] != DBNull.Value ? row["Category"].ToString() : "Engineering",
                            Price = Convert.ToDecimal(row["Price"]),
                            EnrolledCount = Convert.ToInt32(row["EnrolledCount"]),
                            ModuleCount = Convert.ToInt32(row["ModuleCount"])
                        };
                        vm.Courses.Add(cItem);

                        if (vm.CurrentCourseId.HasValue && vm.CurrentCourseId.Value == cItem.CourseId)
                        {
                            vm.CurrentCourseTitle = cItem.Title;
                        }

                        // Add to Search Catalog
                        vm.SearchItems.Add(new InstructorSearchItem
                        {
                            Title = cItem.Title,
                            Category = "Course",
                            Subtitle = $"{cItem.Category} &bull; {cItem.ModuleCount} Modules &bull; {cItem.EnrolledCount} Students",
                            TargetUrl = $"/Instructor/Modules?courseId={cItem.CourseId}",
                            IconType = "book"
                        });
                    }
                }

                // 3. Pending Submissions Count & Total
                string subCountSql = @"
                    SELECT 
                        COUNT(*) AS TotalSubmissions,
                        SUM(CASE WHEN s.Status LIKE '%Pending%' THEN 1 ELSE 0 END) AS PendingCount
                    FROM LMS.StudentSubmissions s
                    JOIN LMS.Courses c ON s.CourseId = c.CourseId
                    WHERE c.InstructorId = @UserId;";

                DataTable dtSubCount = DbHelper.ExecuteDataTable(subCountSql, new[] { new SqlParameter("@UserId", user.UserId) });
                if (dtSubCount != null && dtSubCount.Rows.Count > 0)
                {
                    vm.TotalSubmissionsCount = dtSubCount.Rows[0]["TotalSubmissions"] != DBNull.Value ? Convert.ToInt32(dtSubCount.Rows[0]["TotalSubmissions"]) : 0;
                    vm.PendingSubmissionsCount = dtSubCount.Rows[0]["PendingCount"] != DBNull.Value ? Convert.ToInt32(dtSubCount.Rows[0]["PendingCount"]) : 0;
                }

                // 4. Total Enrolled Students
                string studentSql = @"
                    SELECT COUNT(DISTINCT e.UserId) AS TotalStudents
                    FROM LMS.Enrollments e
                    JOIN LMS.Courses c ON e.CourseId = c.CourseId
                    WHERE c.InstructorId = @UserId;";

                DataTable dtStudents = DbHelper.ExecuteDataTable(studentSql, new[] { new SqlParameter("@UserId", user.UserId) });
                if (dtStudents != null && dtStudents.Rows.Count > 0 && dtStudents.Rows[0]["TotalStudents"] != DBNull.Value)
                {
                    vm.TotalStudentsCount = Convert.ToInt32(dtStudents.Rows[0]["TotalStudents"]);
                }

                // 5. Total Revenue
                string revSql = @"
                    SELECT ISNULL(SUM(p.Amount), 0) AS GrossRev
                    FROM LMS.Payments p
                    JOIN LMS.Courses c ON p.CourseId = c.CourseId
                    WHERE c.InstructorId = @UserId;";

                DataTable dtRev = DbHelper.ExecuteDataTable(revSql, new[] { new SqlParameter("@UserId", user.UserId) });
                if (dtRev != null && dtRev.Rows.Count > 0 && dtRev.Rows[0]["GrossRev"] != DBNull.Value)
                {
                    vm.GrossRevenue = Convert.ToDecimal(dtRev.Rows[0]["GrossRev"]);
                    vm.NetEarnings = Math.Round(vm.GrossRevenue * 0.90m, 2);
                }

                // 6. Recent Submissions for Notifications & Search
                string recSubSql = @"
                    SELECT TOP 8 s.SubmissionId, s.CourseId, c.Title AS CourseTitle, u.FullName, s.SubmissionTitle, s.SubmittedAt, s.Status
                    FROM LMS.StudentSubmissions s
                    JOIN LMS.Courses c ON s.CourseId = c.CourseId
                    JOIN LMS.Users u ON s.UserId = u.UserId
                    WHERE c.InstructorId = @UserId
                    ORDER BY s.SubmittedAt DESC;";

                DataTable dtRec = DbHelper.ExecuteDataTable(recSubSql, new[] { new SqlParameter("@UserId", user.UserId) });
                if (dtRec != null)
                {
                    foreach (DataRow row in dtRec.Rows)
                    {
                        string status = row["Status"].ToString();
                        bool isPending = status.IndexOf("Pending", StringComparison.OrdinalIgnoreCase) >= 0;
                        string sTitle = row["SubmissionTitle"].ToString();
                        string studentName = row["FullName"].ToString();
                        string cTitle = row["CourseTitle"].ToString();
                        DateTime subAt = Convert.ToDateTime(row["SubmittedAt"]);

                        vm.Notifications.Add(new InstructorNavbarNotificationItem
                        {
                            NotificationId = Convert.ToInt32(row["SubmissionId"]),
                            Type = isPending ? "Submission" : "Graded",
                            Title = isPending ? $"Review Pending: {studentName}" : $"Graded: {studentName}",
                            Message = $"{sTitle} in {cTitle}",
                            TimestampFormatted = FormatRelativeTime(subAt),
                            TargetUrl = $"/Instructor/Submissions?courseId={row["CourseId"]}",
                            IsUrgent = isPending,
                            Category = "submissions"
                        });

                        // Add to Search
                        vm.SearchItems.Add(new InstructorSearchItem
                        {
                            Title = $"{studentName} — {sTitle}",
                            Category = "Submission",
                            Subtitle = $"{cTitle} &bull; {status}",
                            TargetUrl = $"/Instructor/Submissions?courseId={row["CourseId"]}",
                            IconType = "file"
                        });
                    }
                }

                // 7. Enrolled Students for Search
                string enrolledSql = @"
                    SELECT DISTINCT TOP 10 u.UserId, u.FullName, u.Email, c.Title AS CourseTitle, c.CourseId
                    FROM LMS.Enrollments e
                    JOIN LMS.Courses c ON e.CourseId = c.CourseId
                    JOIN LMS.Users u ON e.UserId = u.UserId
                    WHERE c.InstructorId = @UserId;";

                DataTable dtEnrolled = DbHelper.ExecuteDataTable(enrolledSql, new[] { new SqlParameter("@UserId", user.UserId) });
                if (dtEnrolled != null)
                {
                    foreach (DataRow row in dtEnrolled.Rows)
                    {
                        vm.SearchItems.Add(new InstructorSearchItem
                        {
                            Title = row["FullName"].ToString(),
                            Category = "Student",
                            Subtitle = $"{row["Email"]} &bull; Enrolled in {row["CourseTitle"]}",
                            TargetUrl = $"/Instructor/Submissions?courseId={row["CourseId"]}",
                            IconType = "user"
                        });
                    }
                }

                // Add built-in Quick Workspace Actions to Search
                vm.SearchItems.Add(new InstructorSearchItem { Title = "Create New Course", Category = "Action", Subtitle = "Publish a new curriculum", TargetUrl = "/Instructor/Create", IconType = "plus" });
                vm.SearchItems.Add(new InstructorSearchItem { Title = "Manage Curriculum & Modules", Category = "Action", Subtitle = "Upload lectures, PDFs, and assignments", TargetUrl = "/Instructor/Modules", IconType = "layers" });
                vm.SearchItems.Add(new InstructorSearchItem { Title = "AI Quiz Generator", Category = "Action", Subtitle = "Generate automated assessments from PDF slides", TargetUrl = "/Instructor/ManageQuiz", IconType = "cpu" });
                vm.SearchItems.Add(new InstructorSearchItem { Title = "Assignment Grading Dossiers", Category = "Action", Subtitle = "Review student code and assign marks", TargetUrl = "/Instructor/Submissions", IconType = "check" });
                vm.SearchItems.Add(new InstructorSearchItem { Title = "Instructor Verification Profile", Category = "Action", Subtitle = "Profile pedigree & credentials", TargetUrl = "/Instructor/Profile", IconType = "user" });

                // Calculate unread count
                vm.UnreadNotificationsCount = vm.PendingSubmissionsCount;
            }
            catch
            {
                // Resilient fallback defaults
                vm.PendingSubmissionsCount = 0;
            }

            return vm;
        }

        private static string FormatRelativeTime(DateTime dt)
        {
            var span = DateTime.Now - dt;
            if (span.TotalMinutes < 60) return $"{(int)Math.Max(1, span.TotalMinutes)}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return dt.ToString("MMM dd");
        }
    }

    public class InstructorNavbarCourseItem
    {
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public int EnrolledCount { get; set; }
        public int ModuleCount { get; set; }
    }

    public class InstructorNavbarNotificationItem
    {
        public int NotificationId { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string TimestampFormatted { get; set; }
        public string TargetUrl { get; set; }
        public bool IsUrgent { get; set; }
        public string Category { get; set; }
    }

    public class InstructorSearchItem
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public string Subtitle { get; set; }
        public string TargetUrl { get; set; }
        public string IconType { get; set; }
    }
}
