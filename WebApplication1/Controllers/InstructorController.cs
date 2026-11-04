using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using WebApplication1.Models;
using WebApplication1.Repositories;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class InstructorController : Controller
    {
        private readonly ICourseRepository _courseRepo;
        private readonly IQuizRepository _quizRepo;
        private readonly IPaymentRepository _paymentRepo;
        private readonly IInstructorRepository _instructorRepo;
        private readonly IUserRepository _userRepo;

        public InstructorController()
        {
            _courseRepo = new CourseRepository();
            _quizRepo = new QuizRepository();
            _paymentRepo = new PaymentRepository();
            _instructorRepo = new InstructorRepository();
            _userRepo = new UserRepository();
        }

        public InstructorController(ICourseRepository courseRepo, IQuizRepository quizRepo, IPaymentRepository paymentRepo, IInstructorRepository instructorRepo, IUserRepository userRepo)
        {
            _courseRepo = courseRepo;
            _quizRepo = quizRepo;
            _paymentRepo = paymentRepo;
            _instructorRepo = instructorRepo;
            _userRepo = userRepo;
        }

        private int? GetCurrentUserId()
        {
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int id))
            {
                return id;
            }
            return null;
        }

        private bool IsInstructor()
        {
            string role = Session["UserRole"] as string;
            return role == "Instructor" || role == "Admin";
        }

        private bool IsApproved()
        {
            if (Session["UserRole"] as string == "Admin") return true;
            return Session["IsApproved"] != null && Convert.ToBoolean(Session["IsApproved"]);
        }

        // ====================================================================
        // INSTRUCTOR STUDIO DASHBOARD
        // ====================================================================
        // GET: /Instructor
        public ActionResult Index()
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor())
            {
                TempData["WarningMessage"] = "Please sign in as an instructor to access the Instructor Studio.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Instructor") });
            }

            if (!IsApproved())
            {
                TempData["WarningMessage"] = "Your instructor account is pending administrator verification. Please complete your professional profile and address any reviewer feedback to unlock course publishing.";
                return RedirectToAction("Profile");
            }

            ViewBag.IsApproved = IsApproved();

            // Fetch profile and check completion gate
            var profile = _instructorRepo.GetProfile(userId.Value);
            ViewBag.Profile = profile;
            ViewBag.CanUploadCourse = profile != null && profile.CanUploadCourse;

            var myCourses = _courseRepo.GetCoursesByInstructor(userId.Value).ToList();

            // Fetch pending assignments count
            try
            {
                var submissions = _instructorRepo.GetSubmissionsForInstructor(userId.Value).ToList();
                ViewBag.Submissions = submissions;
                ViewBag.PendingSubmissionsCount = submissions.Count(s => s.IsPending);
            }
            catch
            {
                ViewBag.Submissions = new List<StudentSubmission>();
                ViewBag.PendingSubmissionsCount = 0;
            }

            // Financial summary: Gross revenue, 10% platform fee, 90% net earnings
            try
            {
                var salesInvoices = _paymentRepo.GetInvoicesByInstructor(userId.Value).ToList();
                decimal grossRevenue = salesInvoices.Sum(i => i.Amount);
                decimal platformFee = Math.Round(grossRevenue * 0.10m, 2);
                decimal netEarnings = grossRevenue - platformFee;

                ViewBag.GrossRevenue = grossRevenue;
                ViewBag.PlatformFee = platformFee;
                ViewBag.NetEarnings = netEarnings;
                ViewBag.TotalSalesCount = salesInvoices.Count;
                ViewBag.SalesInvoices = salesInvoices;
            }
            catch
            {
                ViewBag.GrossRevenue = 0m;
                ViewBag.PlatformFee = 0m;
                ViewBag.NetEarnings = 0m;
                ViewBag.TotalSalesCount = 0;
                ViewBag.SalesInvoices = new List<InvoiceViewModel>();
            }

            return View(myCourses);
        }

        // ====================================================================
        // INSTRUCTOR PROFILE & 90% COMPLETION GATE
        // ====================================================================
        // GET: /Instructor/Profile
        [HttpGet]
        public new ActionResult Profile()
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor())
            {
                return RedirectToAction("Login", "Account");
            }

            var profile = _instructorRepo.GetProfile(userId.Value);
            return View(profile);
        }

        // POST: /Instructor/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public new ActionResult Profile(InstructorProfile model, HttpPostedFileBase profilePhoto, HttpPostedFileBase digitalSignatureFile, string signatureDataUrl)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor())
            {
                return RedirectToAction("Login", "Account");
            }

            model.UserId = userId.Value;

            try
            {
                // Upload Profile Photo via Cloudinary (or local fallback)
                if (profilePhoto != null && profilePhoto.ContentLength > 0)
                {
                    string uploadedUrl = CloudinaryService.UploadImage(profilePhoto, "profiles");
                    if (!string.IsNullOrWhiteSpace(uploadedUrl))
                    {
                        model.ProfilePictureUrl = uploadedUrl;
                    }
                }

                // Upload Digital Signature File via Cloudinary (or local fallback)
                if (digitalSignatureFile != null && digitalSignatureFile.ContentLength > 0)
                {
                    string uploadedSig = CloudinaryService.UploadImage(digitalSignatureFile, "signatures");
                    if (!string.IsNullOrWhiteSpace(uploadedSig))
                    {
                        model.DigitalSignatureUrl = uploadedSig;
                    }
                }
                // Handle canvas-drawn digital signature (Base64 data URL)
                else if (!string.IsNullOrWhiteSpace(signatureDataUrl) && signatureDataUrl.StartsWith("data:image"))
                {
                    try
                    {
                        string base64Data = signatureDataUrl.Substring(signatureDataUrl.IndexOf(",") + 1);
                        byte[] imageBytes = Convert.FromBase64String(base64Data);
                        string fileName = "sig_" + Guid.NewGuid().ToString("N") + ".png";
                        string relativeDir = "/Uploads/signatures/";
                        string serverDir = Server.MapPath("~" + relativeDir);

                        if (!Directory.Exists(serverDir)) Directory.CreateDirectory(serverDir);
                        string filePath = Path.Combine(serverDir, fileName);
                        System.IO.File.WriteAllBytes(filePath, imageBytes);

                        model.DigitalSignatureUrl = relativeDir + fileName;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Canvas signature parse error: " + ex.Message);
                    }
                }

                var updated = _instructorRepo.SaveProfile(model);

                if (updated.CanUploadCourse)
                {
                    TempData["SuccessMessage"] = $"Profile updated successfully! Congratulations, your profile is {updated.CompletionPercentage}% complete. Course creation is now UNLOCKED!";
                }
                else
                {
                    TempData["WarningMessage"] = $"Profile updated ({updated.CompletionPercentage}% complete). Remember, you need at least 90% profile completion before you can publish courses.";
                }

                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Failed to save profile: " + ex.Message);
                return View(model);
            }
        }

        // ====================================================================
        // COURSE CREATION (WITH STRICT 90% PROFILE COMPLETION GATE)
        // ====================================================================
        // GET: /Instructor/Create
        [HttpGet]
        public ActionResult Create()
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor())
            {
                return RedirectToAction("Login", "Account");
            }

            if (!IsApproved())
            {
                TempData["ErrorMessage"] = "Your instructor account is pending administrator approval. You cannot publish courses until verified.";
                return RedirectToAction("Index");
            }

            // STRICT GATE: Check Profile Completion >= 90%
            var profile = _instructorRepo.GetProfile(userId.Value);
            if (profile == null || !profile.CanUploadCourse)
            {
                int currentPct = profile?.CompletionPercentage ?? 0;
                TempData["ErrorMessage"] = $"Course Creation Locked: Your profile is currently {currentPct}% complete. You must reach at least 90% profile completion (including education, teaching experience, and digital signature) before you can upload or publish courses.";
                return RedirectToAction("Profile");
            }

            ViewBag.ExistingCategories = _courseRepo.GetCourseCategories().Select(c => c.CategoryName).ToList();

            var model = new Course
            {
                DurationWeeks = 6,
                Level = profile?.TargetLevel ?? "Beginner",
                Category = "Frontend",
                Price = profile?.ProposedPricing ?? 49.99m
            };

            return View(model);
        }

        // POST: /Instructor/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Course model)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor())
            {
                return RedirectToAction("Login", "Account");
            }

            if (!IsApproved())
            {
                TempData["ErrorMessage"] = "Account verification is pending. Course creation is disabled.";
                return RedirectToAction("Index");
            }

            // STRICT GATE: Verify 90% Profile Completion again on POST
            var profile = _instructorRepo.GetProfile(userId.Value);
            if (profile == null || !profile.CanUploadCourse)
            {
                int currentPct = profile?.CompletionPercentage ?? 0;
                TempData["ErrorMessage"] = $"Course Creation Locked: Your profile is {currentPct}% complete. You cannot publish courses until your profile completion reaches at least 90%.";
                return RedirectToAction("Profile");
            }

            model.InstructorId = userId.Value;
            model.InstructorName = Session["UserName"] as string ?? "Instructor";
            ModelState.Remove("InstructorName");
            ModelState.Remove("InstructorId");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var created = _courseRepo.CreateCourse(model);
                if (created != null)
                {
                    TempData["SuccessMessage"] = $"Course '{created.Title}' created successfully! Now add curriculum modules below.";
                    return RedirectToAction("Modules", new { id = created.CourseId });
                }

                ModelState.AddModelError("", "Failed to publish course. Please try again.");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        // GET: /Instructor/Edit/5
        [HttpGet]
        public ActionResult Edit(int id)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            var course = _courseRepo.GetCourseById(id);
            if (course == null) return HttpNotFound("Course not found.");

            if (course.InstructorId != userId.Value && Session["UserRole"] as string != "Admin")
            {
                TempData["ErrorMessage"] = "You do not have permission to edit this course.";
                return RedirectToAction("Index");
            }

            ViewBag.ExistingCategories = _courseRepo.GetCourseCategories().Select(c => c.CategoryName).ToList();
            return View(course);
        }

        // POST: /Instructor/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Course model)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            model.InstructorId = userId.Value;
            model.InstructorName = Session["UserName"] as string ?? "Instructor";
            ModelState.Remove("InstructorName");
            ModelState.Remove("InstructorId");

            if (!ModelState.IsValid) return View(model);

            try
            {
                var existing = _courseRepo.GetCourseById(model.CourseId);
                if (existing == null) return HttpNotFound();

                if (existing.InstructorId != userId.Value && Session["UserRole"] as string != "Admin")
                {
                    TempData["ErrorMessage"] = "Unauthorized edit attempt.";
                    return RedirectToAction("Index");
                }

                model.InstructorId = existing.InstructorId;
                model.InstructorName = existing.InstructorName;

                bool success = _courseRepo.UpdateCourse(model);
                if (success)
                {
                    TempData["SuccessMessage"] = "Course details updated successfully!";
                    return RedirectToAction("Index");
                }

                ModelState.AddModelError("", "Failed to update course.");
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        // POST: /Instructor/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            var course = _courseRepo.GetCourseById(id);
            if (course == null) return HttpNotFound();

            if (course.InstructorId != userId.Value && Session["UserRole"] as string != "Admin")
            {
                TempData["ErrorMessage"] = "You do not have permission to delete this course.";
                return RedirectToAction("Index");
            }

            if (course.TotalEnrolled > 0 && Session["UserRole"] as string != "Admin")
            {
                TempData["ErrorMessage"] = $"Cannot delete course '{course.Title}' because it has {course.TotalEnrolled} active enrolled learner(s).";
                return RedirectToAction("Index");
            }

            bool success = _courseRepo.DeleteCourse(id);
            if (success)
            {
                TempData["SuccessMessage"] = $"Course '{course.Title}' deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete course.";
            }

            return RedirectToAction("Index");
        }

        // ====================================================================
        // DYNAMIC COURSE MODULES & CURRICULUM MANAGEMENT
        // ====================================================================
        // GET: /Instructor/Modules/5
        [HttpGet]
        public ActionResult Modules(int id)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            var course = _courseRepo.GetCourseById(id);
            if (course == null) return HttpNotFound();

            if (course.InstructorId != userId.Value && Session["UserRole"] as string != "Admin")
            {
                TempData["ErrorMessage"] = "Unauthorized access to course modules.";
                return RedirectToAction("Index");
            }

            ViewBag.Course = course;
            var modules = _instructorRepo.GetModulesByCourse(id).ToList();
            return View(modules);
        }

        // POST: /Instructor/SaveModule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveModule(CourseModule model, HttpPostedFileBase materialFile = null)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            var course = _courseRepo.GetCourseById(model.CourseId);
            if (course == null || (course.InstructorId != userId.Value && Session["UserRole"] as string != "Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized module modification.";
                return RedirectToAction("Index");
            }

            // Handle optional uploaded study material file (video or PDF)
            if (materialFile != null && materialFile.ContentLength > 0)
            {
                var allowedExts = new[] { ".pdf", ".mp4", ".webm", ".mov", ".mkv", ".docx", ".zip" };
                string ext = Path.GetExtension(materialFile.FileName)?.ToLower();

                if (string.IsNullOrEmpty(ext) || !allowedExts.Contains(ext))
                {
                    TempData["ErrorMessage"] = "Unsupported file type. Allowed formats: PDF, MP4, WebM, MOV, DOCX, ZIP.";
                    return RedirectToAction("Modules", new { id = model.CourseId });
                }

                if (materialFile.ContentLength > 100 * 1024 * 1024) // 100 MB max
                {
                    TempData["ErrorMessage"] = "Uploaded file exceeds the maximum 100 MB limit.";
                    return RedirectToAction("Modules", new { id = model.CourseId });
                }

                try
                {
                    bool isVideo = ext == ".mp4" || ext == ".webm" || ext == ".mov" || ext == ".mkv";
                    string targetFolder = isVideo ? "~/Uploads/Videos" : "~/Uploads/CourseMaterials";
                    string physicalDir = Server.MapPath(targetFolder);
                    if (!Directory.Exists(physicalDir)) Directory.CreateDirectory(physicalDir);

                    string rawName = Path.GetFileNameWithoutExtension(materialFile.FileName);
                    string safeName = System.Text.RegularExpressions.Regex.Replace(rawName, @"[^a-zA-Z0-9_\-]", "_");
                    string uniqueFileName = $"Course{model.CourseId}_M{model.ModuleNumber}_{DateTime.UtcNow.Ticks}_{safeName}{ext}";
                    string fullPath = Path.Combine(physicalDir, uniqueFileName);
                    materialFile.SaveAs(fullPath);

                    model.VideoOrResourceUrl = (isVideo ? "/Uploads/Videos/" : "/Uploads/CourseMaterials/") + uniqueFileName;

                    // Automatically trigger live RAG vector indexing into ChromaDB for PDF study materials
                    if (ext == ".pdf")
                    {
                        string capturedFullPath = fullPath;
                        int capturedCourseId = model.CourseId;
                        string capturedFileName = uniqueFileName;
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            try
                            {
                                using (var ragClient = new System.Net.Http.HttpClient())
                                {
                                    ragClient.Timeout = TimeSpan.FromSeconds(30);
                                    using (var formContent = new System.Net.Http.MultipartFormDataContent())
                                    {
                                        byte[] fileBytes = System.IO.File.ReadAllBytes(capturedFullPath);
                                        var byteContent = new System.Net.Http.ByteArrayContent(fileBytes);
                                        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                                        formContent.Add(byteContent, "file", capturedFileName);
                                        formContent.Add(new System.Net.Http.StringContent(capturedCourseId.ToString()), "course_id");
                                        formContent.Add(new System.Net.Http.StringContent("student"), "visibility");
                                        formContent.Add(new System.Net.Http.StringContent("lecture_notes"), "doc_type");
                                        await ragClient.PostAsync("http://127.0.0.1:8000/api/ingest-study-material", formContent);
                                    }
                                }
                            }
                            catch (Exception ragEx)
                            {
                                System.Diagnostics.Debug.WriteLine("[RAG Live Study Material Ingestion Error] " + ragEx.Message);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Material upload failed: " + ex.Message;
                    return RedirectToAction("Modules", new { id = model.CourseId });
                }
            }

            if (ModelState.IsValid)
            {
                var result = _instructorRepo.SaveModule(model);
                if (result.IsSuccess)
                {
                    TempData["SuccessMessage"] = $"Module #{model.ModuleNumber} ('{model.Title}') saved successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please fill in all required module details.";
            }

            return RedirectToAction("Modules", new { id = model.CourseId });
        }

        // POST: /Instructor/DeleteModule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteModule(int moduleId, int courseId)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            var course = _courseRepo.GetCourseById(courseId);
            if (course == null || (course.InstructorId != userId.Value && Session["UserRole"] as string != "Admin"))
            {
                TempData["ErrorMessage"] = "Unauthorized module deletion.";
                return RedirectToAction("Index");
            }

            var result = _instructorRepo.DeleteModule(moduleId, courseId);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "Module removed from curriculum.";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Modules", new { id = courseId });
        }

        // ====================================================================
        // ASSIGNMENT / QUIZ REVIEW & GRADING WORKFLOW
        // ====================================================================
        // GET: /Instructor/Submissions
        [HttpGet]
        public ActionResult Submissions()
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            var submissions = _instructorRepo.GetSubmissionsForInstructor(userId.Value).ToList();
            return View(submissions);
        }

        // POST: /Instructor/GradeSubmission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GradeSubmission(GradeSubmissionViewModel model)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                var result = _instructorRepo.GradeSubmission(model.SubmissionId, model.MarksObtained, model.InstructorFeedback, model.Status, userId.Value);
                if (result.IsSuccess)
                {
                    TempData["SuccessMessage"] = $"Submission successfully reviewed! Marks ({model.MarksObtained}/100) and feedback have been released to the student.";
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please provide valid marks (0-100) and review feedback.";
            }

            return RedirectToAction("Submissions");
        }

        // ====================================================================
        // QUIZ MANAGEMENT
        // ====================================================================
        // GET: /Instructor/ManageQuiz/5
        [HttpGet]
        public ActionResult ManageQuiz(int id)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            var course = _courseRepo.GetCourseById(id);
            if (course == null) return HttpNotFound();

            if (course.InstructorId != userId.Value && Session["UserRole"] as string != "Admin")
            {
                TempData["ErrorMessage"] = "Unauthorized quiz management.";
                return RedirectToAction("Index");
            }

            ViewBag.Course = course;
            var questions = _quizRepo.GetAllQuestionsForCourse(id);
            return View(questions);
        }

        // POST: /Instructor/AddQuizQuestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddQuizQuestion(QuizQuestion model)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            if (ModelState.IsValid)
            {
                var result = _quizRepo.CreateQuestion(model);
                if (result.IsSuccess)
                {
                    TempData["SuccessMessage"] = "Quiz question added successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please fill in all question fields and options.";
            }

            return RedirectToAction("ManageQuiz", new { id = model.CourseId });
        }

        // POST: /Instructor/DeleteQuizQuestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteQuizQuestion(int questionId, int courseId)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor()) return RedirectToAction("Login", "Account");

            var result = _quizRepo.DeleteQuestion(questionId);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "Quiz question removed.";
            }

            return RedirectToAction("ManageQuiz", new { id = courseId });
        }

        // ====================================================================
        // AI PDF-TO-QUIZ GENERATOR
        // ====================================================================
        // POST: /Instructor/GenerateQuizFromPdf
        [HttpPost]
        public async Task<ActionResult> GenerateQuizFromPdf(int courseId, int lessonNumber, int numQuestions, HttpPostedFileBase quizPdf, string topic = "")
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor())
            {
                return Json(new { success = false, message = "Unauthorized access. Please sign in as an instructor." });
            }

            var course = _courseRepo.GetCourseById(courseId);
            if (course == null || (course.InstructorId != userId.Value && Session["UserRole"] as string != "Admin"))
            {
                return Json(new { success = false, message = "Unauthorized course access." });
            }

            if (quizPdf == null || quizPdf.ContentLength == 0)
            {
                return Json(new { success = false, message = "Please select a valid PDF lecture or syllabus document." });
            }

            string ext = Path.GetExtension(quizPdf.FileName)?.ToLower();
            if (ext != ".pdf")
            {
                return Json(new { success = false, message = "Only PDF (.pdf) documents are supported." });
            }

            try
            {
                // Save a permanent reference copy to Uploads/CourseMaterials so students can download it as study material
                try
                {
                    string materialsDir = Server.MapPath("~/Uploads/CourseMaterials");
                    if (!Directory.Exists(materialsDir)) Directory.CreateDirectory(materialsDir);
                    string cleanFileName = Path.GetFileNameWithoutExtension(quizPdf.FileName);
                    cleanFileName = System.Text.RegularExpressions.Regex.Replace(cleanFileName, @"[^a-zA-Z0-9_\-]", "_");
                    string saveName = $"Course{courseId}_Lesson{lessonNumber}_{cleanFileName}.pdf";
                    string destPath = Path.Combine(materialsDir, saveName);
                    quizPdf.SaveAs(destPath);
                    quizPdf.InputStream.Position = 0;
                }
                catch { }

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(60);
                    using (var content = new MultipartFormDataContent())
                    {
                        var fileBytes = new byte[quizPdf.ContentLength];
                        quizPdf.InputStream.Read(fileBytes, 0, quizPdf.ContentLength);
                        var fileContent = new ByteArrayContent(fileBytes);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                        content.Add(fileContent, "file", Path.GetFileName(quizPdf.FileName));
                        content.Add(new StringContent(numQuestions.ToString()), "num_questions");
                        content.Add(new StringContent(lessonNumber.ToString()), "lesson_number");
                        if (!string.IsNullOrWhiteSpace(topic))
                        {
                            content.Add(new StringContent(topic), "topic");
                        }

                        var response = await client.PostAsync("http://127.0.0.1:8000/api/generate-quiz-from-pdf", content);
                        string jsonResult = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            return Json(new { success = false, message = "RAG AI service returned error: " + jsonResult });
                        }

                        return Content(jsonResult, "application/json");
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "AI Quiz generation failed: " + ex.Message });
            }
        }

        // POST: /Instructor/SaveGeneratedQuestions
        [HttpPost]
        public ActionResult SaveGeneratedQuestions(int courseId, int lessonNumber, List<QuizQuestion> questions)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue || !IsInstructor())
            {
                return Json(new { success = false, message = "Unauthorized access." });
            }

            var course = _courseRepo.GetCourseById(courseId);
            if (course == null || (course.InstructorId != userId.Value && Session["UserRole"] as string != "Admin"))
            {
                return Json(new { success = false, message = "Unauthorized course access." });
            }

            if (questions == null || !questions.Any())
            {
                return Json(new { success = false, message = "No questions selected to save." });
            }

            int savedCount = 0;
            foreach (var q in questions)
            {
                if (string.IsNullOrWhiteSpace(q.QuestionText) || string.IsNullOrWhiteSpace(q.OptionA))
                    continue;

                q.CourseId = courseId;
                q.LessonNumber = lessonNumber;
                if (string.IsNullOrWhiteSpace(q.CorrectOption))
                    q.CorrectOption = "A";

                var res = _quizRepo.CreateQuestion(q);
                if (res.IsSuccess) savedCount++;
            }

            return Json(new { 
                success = true, 
                savedCount = savedCount, 
                message = $"Successfully added {savedCount} AI-generated questions to Lesson {lessonNumber} quiz!" 
            });
        }

        // ====================================================================
        // CERTIFICATE PREVIEW (WITH DIGITAL SIGNATURE)
        // ====================================================================
        // GET: /Instructor/Certificate
        [HttpGet]
        public ActionResult Certificate(int courseId, int? studentId = null)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var course = _courseRepo.GetCourseById(courseId);
            if (course == null) return HttpNotFound();

            int targetStudentId = studentId ?? userId.Value;
            var student = _userRepo.GetUserById(targetStudentId);

            int instructorId = course.InstructorId ?? userId.Value;
            var instructorProfile = _instructorRepo.GetProfile(instructorId);

            ViewBag.Course = course;
            ViewBag.Student = student;
            ViewBag.InstructorProfile = instructorProfile;
            ViewBag.IssueDate = DateTime.Now.ToString("MMMM dd, yyyy");
            ViewBag.CertificateId = "CERT-" + courseId + "-" + targetStudentId + "-" + DateTime.Now.ToString("yyyyMMdd");

            return View();
        }

        // ====================================================================
        // PUBLIC REGISTRATION STATUS & FEEDBACK LOOKUP (NO EMAIL SENDING FACILITY)
        // ====================================================================
        // GET: /Instructor/ApplicationStatus
        [HttpGet]
        [AllowAnonymous]
        public ActionResult ApplicationStatus(string email = null)
        {
            string lookupEmail = email;
            if (string.IsNullOrWhiteSpace(lookupEmail) && Session["UserEmail"] != null)
            {
                lookupEmail = Session["UserEmail"].ToString();
            }

            var model = new InstructorApplicationStatusViewModel();

            if (!string.IsNullOrWhiteSpace(lookupEmail))
            {
                model.Email = lookupEmail.Trim();
                model.IsSearched = true;
                var profile = _instructorRepo.GetInstructorApplicationStatusByEmail(model.Email);
                model.Profile = profile;
                model.IsFound = profile != null;
                if (!model.IsFound)
                {
                    model.SearchMessage = $"No instructor registration found for '{model.Email}'. Please check the email spelling or apply as an instructor.";
                }
            }

            return View(model);
        }

        // POST: /Instructor/ApplicationStatus
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ApplicationStatus(InstructorApplicationStatusViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("Email", "Please enter your registered email address.");
                return View(model ?? new InstructorApplicationStatusViewModel());
            }

            model.Email = model.Email.Trim();
            model.IsSearched = true;
            var profile = _instructorRepo.GetInstructorApplicationStatusByEmail(model.Email);
            model.Profile = profile;
            model.IsFound = profile != null;

            if (!model.IsFound)
            {
                model.SearchMessage = $"No instructor registration found for '{model.Email}'. Please check the email spelling or apply as an instructor.";
            }

            return View(model);
        }
    }
}
