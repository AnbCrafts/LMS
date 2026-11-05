using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using WebApplication1.Models;
using WebApplication1.Repositories;

namespace WebApplication1.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly IInstructorRepository _instructorRepo;
        private readonly ICourseRepository _courseRepo;

        public DashboardController()
        {
            _enrollmentRepo = new EnrollmentRepository();
            _instructorRepo = new InstructorRepository();
            _courseRepo = new CourseRepository();
        }

        public DashboardController(IEnrollmentRepository enrollmentRepo, IInstructorRepository instructorRepo = null, ICourseRepository courseRepo = null)
        {
            _enrollmentRepo = enrollmentRepo;
            _instructorRepo = instructorRepo ?? new InstructorRepository();
            _courseRepo = courseRepo ?? new CourseRepository();
        }

        private int? GetCurrentUserId()
        {
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int userId))
            {
                return userId;
            }
            return null;
        }

        // GET: /Dashboard
        public ActionResult Index()
        {
            int? userId = GetCurrentUserId();

            // If not logged in, redirect to login
            if (!userId.HasValue)
            {
                TempData["WarningMessage"] = "Please sign in to view your learning dashboard.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Dashboard") });
            }

            // Route user based on their role
            string userRole = Session["UserRole"] as string;
            if (string.Equals(userRole, "Instructor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Instructor");
            }
            if (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Admin");
            }

            var enrollments = _enrollmentRepo.GetStudentDashboard(userId.Value).ToList();

            foreach (var e in enrollments)
            {
                try
                {
                    e.GradeSummary = _instructorRepo.GetCourseGradeSummary(userId.Value, e.CourseId);
                    e.Submissions = _instructorRepo.GetStudentSubmissions(userId.Value, e.CourseId).ToList();
                    e.QuizAttempts = _instructorRepo.GetStudentQuizAttempts(userId.Value, e.CourseId).ToList();
                }
                catch
                {
                    e.GradeSummary = new CourseGradeSummary { UserId = userId.Value, CourseId = e.CourseId };
                }
            }

            // 1. Gather all submissions and quiz attempts
            var allSubmissions = new List<StudentSubmission>();
            var allQuizzes = new List<StudentQuizAttempt>();
            try
            {
                allSubmissions = _instructorRepo.GetStudentSubmissions(userId.Value).ToList();
                allQuizzes = _instructorRepo.GetStudentQuizAttempts(userId.Value).ToList();
            }
            catch
            {
                // Graceful fallback
            }
            ViewBag.AllSubmissions = allSubmissions;
            ViewBag.AllQuizAttempts = allQuizzes;

            // 2. Query Recommended Courses (Courses not yet enrolled)
            try
            {
                var allCourses = _courseRepo.GetAllCourses().ToList();
                var enrolledCourseIds = new HashSet<int>(enrollments.Select(x => x.CourseId));
                var recommended = allCourses.Where(c => !enrolledCourseIds.Contains(c.CourseId)).Take(4).ToList();
                ViewBag.RecommendedCourses = recommended;
            }
            catch
            {
                ViewBag.RecommendedCourses = new List<Course>();
            }

            // 3. Build Real Recent Activity Timeline
            var activityList = new List<RecentActivityItem>();
            foreach (var en in enrollments)
            {
                activityList.Add(new RecentActivityItem
                {
                    Title = "Enrolled in " + en.Course,
                    Category = "Curriculum Enrollment",
                    Timestamp = en.EnrolledAt,
                    Icon = "bi-journal-check",
                    CourseId = en.CourseId
                });
            }
            foreach (var sub in allSubmissions)
            {
                activityList.Add(new RecentActivityItem
                {
                    Title = "Project: " + (string.IsNullOrWhiteSpace(sub.ModuleTitle) ? "Module Capstone" : sub.ModuleTitle) + " (" + sub.Status + ")",
                    Category = "Assignment Submission",
                    Timestamp = sub.SubmittedAt,
                    Icon = "bi-file-earmark-code",
                    CourseId = sub.CourseId
                });
            }
            foreach (var q in allQuizzes)
            {
                activityList.Add(new RecentActivityItem
                {
                    Title = "Diagnostic Quiz: " + q.ScoreFormatted,
                    Category = "Assessment",
                    Timestamp = q.AttemptedAt,
                    Icon = "bi-ui-checks",
                    CourseId = q.CourseId
                });
            }
            ViewBag.RecentActivity = activityList.OrderByDescending(a => a.Timestamp).Take(6).ToList();

            // 4. Completed Courses / Earned Certificates
            var completedCertificates = enrollments.Where(e => e.Progress >= 100 || (e.GradeSummary != null && e.GradeSummary.IsPassing)).ToList();
            ViewBag.Certificates = completedCertificates;

            return View(enrollments);
        }

        // POST: /Dashboard/UpdateProgress
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProgress(int courseId, int progressPercentage)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = _enrollmentRepo.UpdateCourseProgress(userId.Value, courseId, progressPercentage);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "Course progress updated successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            string userRole = Session["UserRole"] as string;
            if (string.Equals(userRole, "Instructor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Learn", "Courses", new { id = courseId });
            }

            return RedirectToAction("Index");
        }

        // POST: /Dashboard/Unenroll
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Unenroll(int courseId)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = _enrollmentRepo.UnenrollCourse(userId.Value, courseId);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "You have dropped the course successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
