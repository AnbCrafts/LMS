using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using WebApplication1.Models;
using WebApplication1.Repositories;

namespace WebApplication1.Controllers
{
    public class CoursesController : Controller
    {
        private readonly ICourseRepository _courseRepo;
        private readonly IEnrollmentRepository _enrollmentRepo;
        private readonly IReviewRepository _reviewRepo;
        private readonly IQuizRepository _quizRepo;
        private readonly IInstructorRepository _instructorRepo;

        private static readonly HttpClient _ragHttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:8000/"),
            Timeout = TimeSpan.FromSeconds(45)
        };

        public class AiRecommendRequestDto
        {
            public string Interests { get; set; }
            public string Level { get; set; }
        }

        public CoursesController()
        {
            _courseRepo = new CourseRepository();
            _enrollmentRepo = new EnrollmentRepository();
            _reviewRepo = new ReviewRepository();
            _quizRepo = new QuizRepository();
            _instructorRepo = new InstructorRepository();
        }

        public CoursesController(ICourseRepository courseRepo, IEnrollmentRepository enrollmentRepo, IReviewRepository reviewRepo, IQuizRepository quizRepo, IInstructorRepository instructorRepo = null)
        {
            _courseRepo = courseRepo;
            _enrollmentRepo = enrollmentRepo;
            _reviewRepo = reviewRepo;
            _quizRepo = quizRepo;
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

        // GET: /Courses
        public ActionResult Index(string search = null, string level = null, string category = null, string[] categories = null, string price = null, string sortBy = null, string instructor = null, string duration = null, bool certificate = false)
        {
            // Normalize selected categories from single or multiple parameters
            var selectedCategories = new List<string>();
            if (categories != null && categories.Length > 0)
            {
                foreach (var c in categories)
                {
                    if (!string.IsNullOrWhiteSpace(c))
                    {
                        selectedCategories.AddRange(c.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                selectedCategories.Add(category.Trim());
            }

            selectedCategories = selectedCategories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Fetch dynamic categories with course counts from DB
            var dbCategories = _courseRepo.GetCourseCategories().ToList();
            foreach (var cat in dbCategories)
            {
                cat.IsSelected = selectedCategories.Contains(cat.CategoryName, StringComparer.OrdinalIgnoreCase);
            }

            // Fetch filtered courses from repository
            var filteredCourses = _courseRepo.GetCoursesFiltered(search, selectedCategories, level, price, sortBy).ToList();

            // Refine by instructor if specified
            if (!string.IsNullOrWhiteSpace(instructor) && !instructor.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filteredCourses = filteredCourses.Where(c => string.Equals(c.InstructorName, instructor, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Refine by duration if specified
            if (!string.IsNullOrWhiteSpace(duration) && !duration.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (duration.Equals("Short", StringComparison.OrdinalIgnoreCase))
                    filteredCourses = filteredCourses.Where(c => c.DurationWeeks <= 4).ToList();
                else if (duration.Equals("Medium", StringComparison.OrdinalIgnoreCase))
                    filteredCourses = filteredCourses.Where(c => c.DurationWeeks > 4 && c.DurationWeeks <= 8).ToList();
                else if (duration.Equals("Deep", StringComparison.OrdinalIgnoreCase))
                    filteredCourses = filteredCourses.Where(c => c.DurationWeeks > 8).ToList();
            }

            // Total count of published courses in catalog
            var allCourses = _courseRepo.GetAllCourses().ToList();
            int totalCatalogCount = allCourses.Count();

            // Extract dynamic unique instructors
            var instructorsList = allCourses
                .Where(c => !string.IsNullOrWhiteSpace(c.InstructorName))
                .Select(c => c.InstructorName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            // Fetch user enrollments for progress-awareness
            int? currentUserId = GetCurrentUserId();
            var userEnrollments = new Dictionary<int, StudentDashboardViewModel>();
            if (currentUserId.HasValue)
            {
                try
                {
                    var dashList = _enrollmentRepo.GetStudentDashboard(currentUserId.Value);
                    if (dashList != null)
                    {
                        foreach (var d in dashList)
                        {
                            userEnrollments[d.CourseId] = d;
                        }
                    }
                }
                catch { }
            }

            // Calculate personalized recommendations based on real user history
            var recommended = new List<Course>();
            if (currentUserId.HasValue && userEnrollments.Count > 0)
            {
                var enrolledIds = new HashSet<int>(userEnrollments.Keys);
                var enrolledCats = allCourses.Where(c => enrolledIds.Contains(c.CourseId)).Select(c => c.Category).Distinct().ToList();
                recommended = allCourses
                    .Where(c => !enrolledIds.Contains(c.CourseId))
                    .OrderByDescending(c => enrolledCats.Contains(c.Category))
                    .ThenByDescending(c => c.TotalEnrolled)
                    .Take(3)
                    .ToList();
            }
            else
            {
                recommended = allCourses.OrderByDescending(c => c.TotalEnrolled).Take(3).ToList();
            }

            var viewModel = new CourseFilterViewModel
            {
                Search = search ?? string.Empty,
                SelectedCategories = selectedCategories,
                Level = string.IsNullOrWhiteSpace(level) ? "All" : level,
                PriceFilter = string.IsNullOrWhiteSpace(price) ? "All" : price,
                SortBy = string.IsNullOrWhiteSpace(sortBy) ? "Popular" : sortBy,
                SelectedInstructor = string.IsNullOrWhiteSpace(instructor) ? "All" : instructor,
                DurationFilter = string.IsNullOrWhiteSpace(duration) ? "All" : duration,
                CertificateOnly = certificate,
                AvailableCategories = dbCategories,
                AvailableInstructors = instructorsList,
                Courses = filteredCourses,
                RecommendedCourses = recommended,
                UserEnrollments = userEnrollments,
                TotalCatalogCount = totalCatalogCount
            };

            // Maintain legacy ViewBags for any partials or shared views
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentLevel = viewModel.Level;
            ViewBag.CurrentCategory = selectedCategories.Count == 1 ? selectedCategories[0] : (selectedCategories.Count > 1 ? string.Join(", ", selectedCategories) : "All");
            ViewBag.UserEnrollments = userEnrollments;

            return View(viewModel);
        }

        // POST: /Courses/AiRecommend
        [HttpPost]
        public async Task<ActionResult> AiRecommend(AiRecommendRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Interests))
            {
                return Json(new { success = false, message = "Please describe your field of interest or learning goal." });
            }

            try
            {
                var payload = new
                {
                    interests = dto.Interests.Trim(),
                    learner_level = string.IsNullOrWhiteSpace(dto.Level) ? "All" : dto.Level.Trim(),
                    top_k = 3
                };

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _ragHttpClient.PostAsync("api/recommend-courses", content);

                if (!response.IsSuccessStatusCode)
                {
                    string errBody = await response.Content.ReadAsStringAsync();
                    return Json(new { success = false, message = "AI Course Advisor is temporarily unavailable. " + errBody });
                }

                string responseString = await response.Content.ReadAsStringAsync();
                return Content(responseString, "application/json");
            }
            catch (HttpRequestException)
            {
                return Json(new
                {
                    success = false,
                    message = "Could not reach the AI Advisor microservice on port 8000. Please ensure the Python service is active."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        // GET: /Courses/Details/1
        public ActionResult Details(int id)
        {
            var course = _courseRepo.GetCourseById(id);
            if (course == null)
            {
                return HttpNotFound("Course not found.");
            }

            int? userId = GetCurrentUserId();
            bool isOwner = false;
            bool isEnrolled = false;
            EnrollmentStatusResult enrollmentStatus = null;

            if (userId.HasValue)
            {
                enrollmentStatus = _enrollmentRepo.CheckEnrollment(userId.Value, id);
                isEnrolled = enrollmentStatus != null && enrollmentStatus.IsEnrolled;
                ViewBag.IsEnrolled = isEnrolled;
                ViewBag.Enrollment = enrollmentStatus;

                // Check if the current user is the author/instructor of this course
                isOwner = (course.InstructorId.HasValue && course.InstructorId.Value == userId.Value)
                          || string.Equals(course.InstructorName, Session["UserName"] as string, StringComparison.OrdinalIgnoreCase);

                if (isEnrolled)
                {
                    try
                    {
                        ViewBag.GradeSummary = _instructorRepo.GetCourseGradeSummary(userId.Value, id);
                        ViewBag.StudentSubmissions = _instructorRepo.GetStudentSubmissions(userId.Value, id);
                        ViewBag.StudentQuizAttempts = _instructorRepo.GetStudentQuizAttempts(userId.Value, id);
                        ViewBag.AllQuizQuestions = _quizRepo.GetAllQuestionsForCourse(id).ToList();
                        ViewBag.Announcements = GetCourseAnnouncements(course);
                    }
                    catch { }
                }
            }
            else
            {
                ViewBag.IsEnrolled = false;
            }
            ViewBag.IsOwner = isOwner;

            // Fetch dynamic modules for this course
            try
            {
                var modules = _instructorRepo.GetModulesByCourse(id).OrderBy(m => m.ModuleNumber).ToList();
                ViewBag.Modules = modules;
            }
            catch
            {
                ViewBag.Modules = new List<CourseModule>();
            }

            // Fetch Instructor Profile if available
            if (course.InstructorId.HasValue)
            {
                try
                {
                    ViewBag.InstructorProfile = _instructorRepo.GetProfile(course.InstructorId.Value);
                }
                catch { }
            }

            // Fetch Reviews
            try
            {
                ViewBag.Reviews = _reviewRepo.GetCourseReviews(id);
            }
            catch
            {
                ViewBag.Reviews = new List<Review>();
            }

            // Fetch Lecture Slides & Study Materials
            try
            {
                string materialsDir = Server.MapPath("~/Uploads/CourseMaterials");
                if (Directory.Exists(materialsDir))
                {
                    var files = Directory.GetFiles(materialsDir, $"Course{id}_*.pdf")
                        .Select(f => new StudyMaterialItem {
                            FileName = Path.GetFileName(f),
                            DisplayName = Path.GetFileNameWithoutExtension(f).Replace($"Course{id}_", "").Replace("_", " "),
                            Url = "/Uploads/CourseMaterials/" + Path.GetFileName(f),
                            FileSizeKb = Math.Max(1, new FileInfo(f).Length / 1024)
                        }).ToList();
                    ViewBag.StudyMaterials = files;
                }
                else
                {
                    ViewBag.StudyMaterials = new List<StudyMaterialItem>();
                }
            }
            catch
            {
                ViewBag.StudyMaterials = new List<StudyMaterialItem>();
            }

            // Fetch Related Courses based on category or instructor
            try
            {
                var related = _courseRepo.GetAllCourses()
                    .Where(c => c.CourseId != id && (string.Equals(c.Category, course.Category, StringComparison.OrdinalIgnoreCase) || string.Equals(c.InstructorName, course.InstructorName, StringComparison.OrdinalIgnoreCase)))
                    .Take(3)
                    .ToList();
                ViewBag.RelatedCourses = related;
            }
            catch
            {
                ViewBag.RelatedCourses = new List<Course>();
            }

            return View(course);
        }

        private List<CourseAnnouncementItem> GetCourseAnnouncements(Course course)
        {
            var list = new List<CourseAnnouncementItem>();
            string instructor = !string.IsNullOrWhiteSpace(course.InstructorName) ? course.InstructorName : "Course Lead";

            list.Add(new CourseAnnouncementItem
            {
                Title = $"{course.Category} Architectural Standards & Next Steps",
                Message = $"Supplementary architecture blueprints, reference slides, and starter code repositories for {course.Title} have been verified and uploaded. Review the Study Materials section below.",
                DateFormatted = "2 days ago",
                AuthorName = instructor,
                Tag = "Curriculum Update",
                TagClass = "bg-primary text-white"
            });

            list.Add(new CourseAnnouncementItem
            {
                Title = "Academic Office Hours & Code Review Workshop",
                Message = "Join the live interactive architecture review this Friday at 4:00 PM UTC. You can query the Academic Copilot at any time to prepare your conceptual questions in advance.",
                DateFormatted = "5 days ago",
                AuthorName = instructor,
                Tag = "Live Session",
                TagClass = "bg-dark text-white"
            });

            list.Add(new CourseAnnouncementItem
            {
                Title = "Practical Assignment Evaluation Criteria",
                Message = "Ensure that your practical assignment submissions include comprehensive code comments and type declarations to achieve maximum marks on instructor dossiers.",
                DateFormatted = "1 week ago",
                AuthorName = instructor,
                Tag = "Notice",
                TagClass = "bg-secondary text-white"
            });

            return list;
        }

        // POST: /Courses/AddReview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddReview(int courseId, int rating, string comment)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = _reviewRepo.AddReview(courseId, userId.Value, rating, comment);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "Thank you for your course review!";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Details", new { id = courseId });
        }

        // POST: /Courses/Enroll/1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Enroll(int id)
        {
            int? userId = GetCurrentUserId();

            if (!userId.HasValue)
            {
                TempData["WarningMessage"] = "Please sign in or create an account to enroll in this course.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", new { id = id }) });
            }

            var course = _courseRepo.GetCourseById(id);
            if (course != null && ((course.InstructorId.HasValue && course.InstructorId.Value == userId.Value)
                || string.Equals(course.InstructorName, Session["UserName"] as string, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["WarningMessage"] = "You are the creator of this course and cannot enroll or purchase your own content.";
                return RedirectToAction("Details", new { id = id });
            }

            // Redirect to Checkout page to simulate payment & generate invoice!
            return RedirectToAction("Checkout", "Payment", new { id = id });
        }

        // GET: /Courses/Learn/1?lesson=1
        public ActionResult Learn(int id, int lesson = 1)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                TempData["WarningMessage"] = "Please sign in to access your course classroom.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Learn", new { id = id }) });
            }

            var course = _courseRepo.GetCourseById(id);
            if (course == null)
            {
                return HttpNotFound("Course not found.");
            }

            var enrollmentStatus = _enrollmentRepo.CheckEnrollment(userId.Value, id);
            bool isOwner = (course.InstructorId.HasValue && course.InstructorId.Value == userId.Value)
                          || string.Equals(course.InstructorName, Session["UserName"] as string, StringComparison.OrdinalIgnoreCase);
            bool isAdmin = Session["UserRole"] as string == "Admin";

            // Allow course author and admin full preview access without enrollment
            if (!enrollmentStatus.IsEnrolled && !isOwner && !isAdmin)
            {
                TempData["WarningMessage"] = "You must enroll in this course before accessing the learning materials.";
                return RedirectToAction("Details", new { id = id });
            }

            ViewBag.IsOwner = isOwner;
            ViewBag.IsAdmin = isAdmin;

            // Build curriculum lessons: dynamically load from LMS.CourseModules if available
            var dbModules = _instructorRepo.GetModulesByCourse(id).ToList();
            var lessons = new List<LessonItem>();

            if (dbModules.Any())
            {
                foreach (var m in dbModules)
                {
                    lessons.Add(new LessonItem
                    {
                        ModuleId = m.ModuleId,
                        LessonNumber = m.ModuleNumber,
                        Title = m.Title,
                        Duration = m.FormattedDuration,
                        Summary = m.Description,
                        ContentMarkdown = m.ContentMarkdown,
                        VideoUrl = m.VideoOrResourceUrl,
                        AssignmentPrompt = m.AssignmentPrompt
                    });
                }
            }
            else
            {
                lessons = new List<LessonItem>
                {
                    new LessonItem
                    {
                        LessonNumber = 1,
                        Title = "Course Introduction & Setup",
                        Duration = "25 mins",
                        Summary = "Setting up tools, understanding foundational architecture, and project organization.",
                        ContentMarkdown = "Welcome to " + course.Title + "! In this initial module, we will explore the foundational concepts, tooling, and best practices that industry engineers use daily."
                    },
                    new LessonItem
                    {
                        LessonNumber = 2,
                        Title = "Core Fundamentals & Components",
                        Duration = "40 mins",
                        Summary = "Hands-on component hierarchies, props, data flows, and clean structuring.",
                        ContentMarkdown = "Dive deep into core primitives. Master modular architecture, state isolation, and re-usable component structures with real production examples."
                    },
                    new LessonItem
                    {
                        LessonNumber = 3,
                        Title = "Advanced State & Async Data Flows",
                        Duration = "45 mins",
                        Summary = "Handling asynchronous requests, global state, REST endpoints, and caching.",
                        ContentMarkdown = "Learn how to build resilient applications by integrating remote APIs, managing asynchronous side-effects, and handling network timeouts gracefully."
                    },
                    new LessonItem
                    {
                        LessonNumber = 4,
                        Title = "Performance Optimization & Testing",
                        Duration = "35 mins",
                        Summary = "Profiler insights, unit testing components, and minimizing renders.",
                        ContentMarkdown = "Fine-tune your application for high performance. Discover memory profiling techniques, automated test cases, and deployment safety guards."
                    },
                    new LessonItem
                    {
                        LessonNumber = 5,
                        Title = "Capstone Project & Certification",
                        Duration = "50 mins",
                        Summary = "Building and deploying a full real-world capstone application.",
                        ContentMarkdown = "Bring all learned concepts together in this capstone assignment. Complete the project build to earn your verified course certificate."
                    }
                };
            }

            int totalModulesCount = Math.Max(1, lessons.Count);
            int currentProgress = enrollmentStatus.ProgressPercentage;

            foreach (var l in lessons)
            {
                l.IsCompleted = (l.LessonNumber * (100 / totalModulesCount)) <= currentProgress;
            }

            if (lesson < 1 || lesson > lessons.Count)
            {
                lesson = 1;
            }

            var activeLesson = lessons[lesson - 1];

            // Fetch student submissions for this course (to see pending review marks)
            StudentSubmission activeModuleSub = null;
            try
            {
                var mySubmissions = _instructorRepo.GetStudentSubmissions(userId.Value, id).ToList();
                ViewBag.MySubmissions = mySubmissions;
                activeModuleSub = mySubmissions.FirstOrDefault(s => (activeLesson.ModuleId > 0 && s.ModuleId == activeLesson.ModuleId) || (s.ModuleNumber.HasValue && s.ModuleNumber == activeLesson.LessonNumber));
                ViewBag.ActiveModuleSubmission = activeModuleSub;
            }
            catch
            {
                ViewBag.MySubmissions = new List<StudentSubmission>();
                ViewBag.ActiveModuleSubmission = null;
            }

            // Fetch quiz questions for this lesson from DB
            var quizQuestions = new List<QuizQuestion>();
            try
            {
                quizQuestions = _quizRepo.GetQuizByCourseAndLesson(id, lesson).ToList();
                ViewBag.QuizQuestions = quizQuestions;
            }
            catch
            {
                ViewBag.QuizQuestions = new List<QuizQuestion>();
            }

            // Prerequisite Gate: Check if current module requires Quiz and/or Assignment
            bool hasQuiz = quizQuestions.Any();
            bool hasPassedQuiz = false;
            try
            {
                var quizAttempts = _instructorRepo.GetStudentQuizAttempts(userId.Value, id).ToList();
                hasPassedQuiz = quizAttempts.Any(a => a.LessonNumber == activeLesson.LessonNumber && a.IsPassed);
                ViewBag.QuizAttempts = quizAttempts;
            }
            catch
            {
                ViewBag.QuizAttempts = new List<StudentQuizAttempt>();
            }

            bool hasAssignment = !string.IsNullOrWhiteSpace(activeLesson.AssignmentPrompt);
            bool hasSubmittedAssignment = activeModuleSub != null;

            bool canAdvanceModule = (!hasQuiz || hasPassedQuiz) && (!hasAssignment || hasSubmittedAssignment);

            ViewBag.HasQuiz = hasQuiz;
            ViewBag.HasPassedQuiz = hasPassedQuiz;
            ViewBag.HasAssignment = hasAssignment;
            ViewBag.HasSubmittedAssignment = hasSubmittedAssignment;
            ViewBag.CanAdvanceModule = canAdvanceModule;

            try
            {
                string materialsDir = Server.MapPath("~/Uploads/CourseMaterials");
                if (Directory.Exists(materialsDir))
                {
                    var files = Directory.GetFiles(materialsDir, $"Course{id}_*.pdf")
                        .Select(f => new StudyMaterialItem {
                            FileName = Path.GetFileName(f),
                            DisplayName = Path.GetFileNameWithoutExtension(f).Replace($"Course{id}_", "").Replace("_", " "),
                            Url = "/Uploads/CourseMaterials/" + Path.GetFileName(f),
                            FileSizeKb = Math.Max(1, new FileInfo(f).Length / 1024)
                        }).ToList();
                    ViewBag.StudyMaterials = files;
                }
            }
            catch { }

            var viewModel = new CoursePlayerViewModel
            {
                Course = course,
                CurrentProgress = currentProgress,
                Status = enrollmentStatus.Status,
                Lessons = lessons,
                ActiveLesson = activeLesson
            };

            return View(viewModel);
        }

        // POST: /Courses/SubmitQuiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitQuiz(int courseId, int lessonNumber, FormCollection form)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var questions = _quizRepo.GetQuizByCourseAndLesson(courseId, lessonNumber).ToList();
            if (!questions.Any())
            {
                // Advance progress anyway if no quiz
                var dbModules = _instructorRepo.GetModulesByCourse(courseId).ToList();
                int totalModules = Math.Max(1, dbModules.Any() ? dbModules.Count : 5);
                int progress = Math.Min(100, lessonNumber * (100 / totalModules));
                _enrollmentRepo.UpdateCourseProgress(userId.Value, courseId, progress);
                TempData["SuccessMessage"] = "Lesson marked as complete!";
                return RedirectToAction("Learn", new { id = courseId, lesson = Math.Min(totalModules, lessonNumber + 1) });
            }

            int correctCount = 0;
            foreach (var q in questions)
            {
                string selected = form["question_" + q.QuestionId];
                if (!string.IsNullOrWhiteSpace(selected) && selected.Trim().Equals(q.CorrectOption.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    correctCount++;
                }
            }

            double percentage = ((double)correctCount / questions.Count) * 100.0;
            bool isPassed = percentage >= 60;

            // Record quiz attempt in database
            try
            {
                _instructorRepo.SaveQuizAttempt(new StudentQuizAttempt
                {
                    UserId = userId.Value,
                    CourseId = courseId,
                    LessonNumber = lessonNumber,
                    TotalQuestions = questions.Count,
                    CorrectAnswers = correctCount,
                    ScorePercentage = (decimal)percentage,
                    IsPassed = isPassed
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SaveQuizAttempt error: " + ex.Message);
            }

            if (isPassed)
            {
                var dbMods = _instructorRepo.GetModulesByCourse(courseId).ToList();
                int totalMods = Math.Max(1, dbMods.Any() ? dbMods.Count : 5);
                var activeMod = dbMods.FirstOrDefault(m => m.ModuleNumber == lessonNumber);
                bool hasAssgn = activeMod != null && !string.IsNullOrWhiteSpace(activeMod.AssignmentPrompt);
                var existingSub = _instructorRepo.GetStudentSubmissions(userId.Value, courseId).FirstOrDefault(s => s.ModuleNumber == lessonNumber);

                if (hasAssgn && existingSub == null)
                {
                    TempData["SuccessMessage"] = $"Awesome! You scored {correctCount}/{questions.Count} ({percentage:0}%) on the quiz! Please also submit the practical assignment below to complete Module {lessonNumber}.";
                }
                else
                {
                    int newProgress = Math.Min(100, lessonNumber * (100 / totalMods));
                    _enrollmentRepo.UpdateCourseProgress(userId.Value, courseId, newProgress);
                    TempData["SuccessMessage"] = $"Awesome! You scored {correctCount}/{questions.Count} ({percentage:0}%) on the quiz. Module {lessonNumber} requirements fulfilled!";
                }

                return RedirectToAction("Learn", new { id = courseId, lesson = lessonNumber });
            }
            else
            {
                TempData["ErrorMessage"] = $"You scored {correctCount}/{questions.Count} ({percentage:0}%). You need 60% or higher to pass the lesson quiz. Please review the lecture notes and try again!";
                return RedirectToAction("Learn", new { id = courseId, lesson = lessonNumber });
            }
        }

        // POST: /Courses/SubmitAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitAssignment(int courseId, int? moduleId = null, int lessonNumber = 1, string submissionTitle = null, string submissionText = null, string submissionFileUrl = null, HttpPostedFileBase submissionFile = null)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            // Prevent Re-submission Gate: Verify if student has already submitted for this assignment
            var existingSubs = _instructorRepo.GetStudentSubmissions(userId.Value, courseId);
            bool alreadySubmitted = existingSubs.Any(s => 
                (moduleId.HasValue && s.ModuleId == moduleId.Value) || 
                (s.ModuleNumber.HasValue && s.ModuleNumber == lessonNumber));

            if (alreadySubmitted)
            {
                TempData["ErrorMessage"] = "You have already submitted this assignment. Re-submissions are strictly not permitted.";
                return RedirectToAction("Learn", new { id = courseId, lesson = lessonNumber });
            }

            string finalFileUrl = submissionFileUrl;

            // Handle uploaded submission file (PDF, ZIP, DOCX, TXT, etc.)
            if (submissionFile != null && submissionFile.ContentLength > 0)
            {
                var allowedExts = new[] { ".pdf", ".zip", ".rar", ".docx", ".doc", ".txt", ".png", ".jpg" };
                string ext = Path.GetExtension(submissionFile.FileName)?.ToLower();
                if (string.IsNullOrEmpty(ext) || !allowedExts.Contains(ext))
                {
                    TempData["ErrorMessage"] = "Unsupported file type. Please upload a PDF, ZIP, RAR, DOCX, or TXT file.";
                    return RedirectToAction("Learn", new { id = courseId, lesson = lessonNumber });
                }

                if (submissionFile.ContentLength > 25 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "Uploaded file exceeds the maximum 25 MB limit.";
                    return RedirectToAction("Learn", new { id = courseId, lesson = lessonNumber });
                }

                try
                {
                    string uploadsDir = Server.MapPath("~/Uploads/Submissions");
                    if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

                    string rawName = Path.GetFileNameWithoutExtension(submissionFile.FileName);
                    string safeName = System.Text.RegularExpressions.Regex.Replace(rawName, @"[^a-zA-Z0-9_\-]", "_");
                    string uniqueFileName = $"Sub_C{courseId}_U{userId.Value}_M{lessonNumber}_{DateTime.UtcNow.Ticks}_{safeName}{ext}";
                    string savePath = Path.Combine(uploadsDir, uniqueFileName);
                    submissionFile.SaveAs(savePath);

                    finalFileUrl = "/Uploads/Submissions/" + uniqueFileName;
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "File upload failed: " + ex.Message;
                    return RedirectToAction("Learn", new { id = courseId, lesson = lessonNumber });
                }
            }

            var sub = new StudentSubmission
            {
                CourseId = courseId,
                ModuleId = moduleId,
                UserId = userId.Value,
                SubmissionTitle = string.IsNullOrWhiteSpace(submissionTitle) ? ($"Lesson {lessonNumber} Practical Task") : submissionTitle.Trim(),
                SubmissionText = submissionText,
                SubmissionFileUrl = finalFileUrl
            };

            var result = _instructorRepo.SubmitAssignment(sub);
            if (result.IsSuccess)
            {
                // Check if this module also has a quiz
                var questions = _quizRepo.GetQuizByCourseAndLesson(courseId, lessonNumber).ToList();
                bool hasQuiz = questions.Any();
                bool passedQuiz = _instructorRepo.GetStudentQuizAttempts(userId.Value, courseId).Any(a => a.LessonNumber == lessonNumber && a.IsPassed);

                var dbMods = _instructorRepo.GetModulesByCourse(courseId).ToList();
                int totalMods = Math.Max(1, dbMods.Any() ? dbMods.Count : 5);

                if (hasQuiz && !passedQuiz)
                {
                    TempData["SuccessMessage"] = "Assignment submitted successfully! Your submission is pending instructor review. Please also complete the lesson quiz above to fulfill Module " + lessonNumber + " requirements.";
                }
                else
                {
                    int stepProgress = Math.Min(100, lessonNumber * (100 / totalMods));
                    _enrollmentRepo.UpdateCourseProgress(userId.Value, courseId, stepProgress);
                    TempData["SuccessMessage"] = "Assignment submitted successfully! Your work is marked as 'Pending Review' by your instructor, and module progress has been updated.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Learn", new { id = courseId, lesson = lessonNumber });
        }

        // GET: /Courses/Certificate/1
        public ActionResult Certificate(int id)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var course = _courseRepo.GetCourseById(id);
            if (course == null) return HttpNotFound();

            var enrollment = _enrollmentRepo.CheckEnrollment(userId.Value, id);
            if (!enrollment.IsEnrolled || enrollment.ProgressPercentage < 100)
            {
                TempData["WarningMessage"] = "You must complete 100% of the course curriculum (including quizzes and assignments) to view and download your certificate.";
                return RedirectToAction("Details", new { id = id });
            }

            var gradeSummary = _instructorRepo.GetCourseGradeSummary(userId.Value, id);
            int instructorId = course.InstructorId ?? 0;
            var instructorProfile = _instructorRepo.GetProfile(instructorId);

            ViewBag.StudentName = Session["UserName"] as string ?? "Student";
            ViewBag.CompletionDate = enrollment.EnrolledAt.HasValue ? enrollment.EnrolledAt.Value.AddDays(14) : DateTime.Now;
            ViewBag.CertificateNumber = "CERT-LMS-" + id.ToString("D4") + "-" + userId.Value.ToString("D4");
            ViewBag.GradeSummary = gradeSummary;
            ViewBag.InstructorProfile = instructorProfile;

            return View(course);
        }
    }
}
