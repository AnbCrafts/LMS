using System.Web.Mvc;
using WebApplication1.Repositories;

namespace WebApplication1.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly IInstructorRepository _instructorRepo;

        public DashboardController()
        {
            _enrollmentRepo = new EnrollmentRepository();
            _instructorRepo = new InstructorRepository();
        }

        public DashboardController(IEnrollmentRepository enrollmentRepo, IInstructorRepository instructorRepo = null)
        {
            _enrollmentRepo = enrollmentRepo;
            _instructorRepo = instructorRepo ?? new InstructorRepository();
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
            if (string.Equals(userRole, "Instructor", System.StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Instructor");
            }
            if (string.Equals(userRole, "Admin", System.StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Admin");
            }

            var enrollments = System.Linq.Enumerable.ToList(_enrollmentRepo.GetStudentDashboard(userId.Value));

            foreach (var e in enrollments)
            {
                try
                {
                    e.GradeSummary = _instructorRepo.GetCourseGradeSummary(userId.Value, e.CourseId);
                    e.Submissions = System.Linq.Enumerable.ToList(_instructorRepo.GetStudentSubmissions(userId.Value, e.CourseId));
                    e.QuizAttempts = System.Linq.Enumerable.ToList(_instructorRepo.GetStudentQuizAttempts(userId.Value, e.CourseId));
                }
                catch
                {
                    e.GradeSummary = new Models.CourseGradeSummary { UserId = userId.Value, CourseId = e.CourseId };
                }
            }

            try
            {
                ViewBag.AllSubmissions = System.Linq.Enumerable.ToList(_instructorRepo.GetStudentSubmissions(userId.Value));
                ViewBag.AllQuizAttempts = System.Linq.Enumerable.ToList(_instructorRepo.GetStudentQuizAttempts(userId.Value));
            }
            catch
            {
                ViewBag.AllSubmissions = new System.Collections.Generic.List<Models.StudentSubmission>();
                ViewBag.AllQuizAttempts = new System.Collections.Generic.List<Models.StudentQuizAttempt>();
            }

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
            if (string.Equals(userRole, "Instructor", System.StringComparison.OrdinalIgnoreCase))
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
