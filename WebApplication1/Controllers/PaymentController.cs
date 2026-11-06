using Hangfire;
using System;
using System.Web.Mvc;
using WebApplication1.Models;
using WebApplication1.Repositories;

namespace WebApplication1.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly ICourseRepository _courseRepo;
        private readonly IEnrollmentRepository _enrollmentRepo;

        public PaymentController()
        {
            _paymentRepo = new PaymentRepository();
            _courseRepo = new CourseRepository();
            _enrollmentRepo = new EnrollmentRepository();
        }

        public PaymentController(IPaymentRepository paymentRepo, ICourseRepository courseRepo, IEnrollmentRepository enrollmentRepo)
        {
            _paymentRepo = paymentRepo;
            _courseRepo = courseRepo;
            _enrollmentRepo = enrollmentRepo;
        }

        private int? GetCurrentUserId()
        {
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int id))
            {
                return id;
            }
            return null;
        }

        // GET: /Payment/Checkout/1
        [HttpGet]
        public ActionResult Checkout(int id)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                TempData["WarningMessage"] = "Please sign in to proceed with checkout.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout", new { id = id }) });
            }

            var course = _courseRepo.GetCourseById(id);
            if (course == null)
            {
                return HttpNotFound("Course not found.");
            }

            if (!course.IsPublished)
            {
                TempData["WarningMessage"] = "This course is currently in draft mode and undergoing curriculum development. It is not open for checkout.";
                return RedirectToAction("Details", "Courses", new { id = id });
            }

            // Check if user is the course author / seller!
            if ((course.InstructorId.HasValue && course.InstructorId.Value == userId.Value)
                || string.Equals(course.InstructorName, Session["UserName"] as string, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WarningMessage"] = "You are the creator of this course. Instructors cannot purchase or enroll in their own courses.";
                return RedirectToAction("Details", "Courses", new { id = id });
            }

            // Check if already enrolled
            var enrollment = _enrollmentRepo.CheckEnrollment(userId.Value, id);
            if (enrollment.IsEnrolled)
            {
                TempData["SuccessMessage"] = "You already own this course!";
                return RedirectToAction("Details", "Courses", new { id = id });
            }

            return View(course);
        }

        // POST: /Payment/Process
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Process(int courseId, string paymentMethod = "Credit Card (Simulated)")
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var course = _courseRepo.GetCourseById(courseId);
            if (course == null)
            {
                return HttpNotFound();
            }

            string txnId = "TXN_SIM_" + DateTime.Now.Ticks.ToString().Substring(10);
            var result = _paymentRepo.ProcessPayment(userId.Value, courseId, course.Price, paymentMethod, txnId);

            if (result.IsSuccess)
            {
                string invoiceNumber = result.Message;

                // Asynchronously process enrollment post-actions (welcome email, syllabus packet, metrics) via Hangfire
                try
                {
                    BackgroundJob.Enqueue(() => WebApplication1.Services.BackgroundJobService.ProcessEnrollmentPostActions(userId.Value, courseId, invoiceNumber));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[Hangfire Enqueue Warning] " + ex.Message);
                }

                TempData["SuccessMessage"] = "Payment successful! Your course enrollment is confirmed.";
                return RedirectToAction("Invoice", new { id = invoiceNumber });
            }

            TempData["ErrorMessage"] = "Payment failed: " + result.Message;
            return RedirectToAction("Checkout", new { id = courseId });
        }

        // GET: /Payment/Invoice/INV-2026-12345
        [HttpGet]
        public ActionResult Invoice(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return HttpNotFound();
            }

            try
            {
                var invoice = _paymentRepo.GetInvoiceDetails(id);
                if (invoice == null)
                {
                    return HttpNotFound("Invoice record not found.");
                }

                // Check authorization: must be the student, or the instructor, or an admin
                int? currentUserId = GetCurrentUserId();
                string userRole = Session["UserRole"] as string;

                if (!currentUserId.HasValue)
                {
                    return RedirectToAction("Login", "Account");
                }

                if (userRole != "Admin" && invoice.StudentId != currentUserId.Value && invoice.InstructorId != currentUserId.Value)
                {
                    TempData["ErrorMessage"] = "Unauthorized invoice access.";
                    return RedirectToAction("Index", "Dashboard");
                }

                return View(invoice);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error retrieving invoice: " + ex.Message;
                return RedirectToAction("Invoices");
            }
        }

        // GET: /Payment/Invoices (Role-based invoice list for User, Instructor, or Admin)
        [HttpGet]
        public ActionResult Invoices()
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            string userRole = Session["UserRole"] as string;

            try
            {
                if (userRole == "Admin")
                {
                    var allInvoices = _paymentRepo.GetAllInvoices();
                    return View("AdminInvoices", allInvoices);
                }
                else if (userRole == "Instructor")
                {
                    var instructorSales = _paymentRepo.GetInvoicesByInstructor(userId.Value);
                    return View("InstructorInvoices", instructorSales);
                }
                else
                {
                    var studentInvoices = _paymentRepo.GetInvoicesByUser(userId.Value);
                    return View("StudentInvoices", studentInvoices);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Could not load invoices: " + ex.Message + ". Please ensure payment stored procedures have been executed in SQL Server.";
                if (userRole == "Admin") return View("AdminInvoices", new System.Collections.Generic.List<InvoiceViewModel>());
                if (userRole == "Instructor") return View("InstructorInvoices", new System.Collections.Generic.List<InvoiceViewModel>());
                return View("StudentInvoices", new System.Collections.Generic.List<InvoiceViewModel>());
            }
        }
    }
}
