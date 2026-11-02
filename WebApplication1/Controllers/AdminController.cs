using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using WebApplication1.Models;
using WebApplication1.Repositories;

namespace WebApplication1.Controllers
{
    public class AdminController : Controller
    {
        private readonly IUserRepository _userRepo;
        private readonly IPaymentRepository _paymentRepo;
        private readonly IInstructorRepository _instructorRepo;

        public AdminController()
        {
            _userRepo = new UserRepository();
            _paymentRepo = new PaymentRepository();
            _instructorRepo = new InstructorRepository();
        }

        public AdminController(IUserRepository userRepo, IPaymentRepository paymentRepo, IInstructorRepository instructorRepo)
        {
            _userRepo = userRepo;
            _paymentRepo = paymentRepo;
            _instructorRepo = instructorRepo;
        }

        private bool IsAdmin()
        {
            return Session["UserRole"] != null && Session["UserRole"].ToString() == "Admin";
        }

        private int? GetCurrentAdminId()
        {
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int uid))
            {
                return uid;
            }
            return null;
        }

        // GET: /Admin
        public ActionResult Index()
        {
            if (!IsAdmin())
            {
                TempData["WarningMessage"] = "Administrator access required. Please log in as admin.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Admin") });
            }

            var allInstructorsWithProfiles = _instructorRepo.GetAllInstructorsWithProfiles().ToList();
            ViewBag.PendingInstructorsWithProfiles = allInstructorsWithProfiles.Where(i => i.IsPendingReview).ToList();
            ViewBag.AllInstructorsWithProfiles = allInstructorsWithProfiles;

            ViewBag.PendingInstructors = _userRepo.GetPendingInstructors();
            ViewBag.AllInstructors = _userRepo.GetAllInstructors();

            // Financial & Platform Revenue (10% Admin Platform Charges)
            try
            {
                var allInvoices = _paymentRepo.GetAllInvoices().ToList();
                decimal totalGrossVolume = allInvoices.Sum(i => i.Amount);
                decimal totalAdminCommission = Math.Round(totalGrossVolume * 0.10m, 2); // 10% Platform Charges
                decimal totalInstructorPayouts = totalGrossVolume - totalAdminCommission; // 90% Course Seller Share

                ViewBag.TotalGrossVolume = totalGrossVolume;
                ViewBag.AdminCommission = totalAdminCommission;
                ViewBag.InstructorPayouts = totalInstructorPayouts;
                ViewBag.TotalOrders = allInvoices.Count;
                ViewBag.AllInvoices = allInvoices;
            }
            catch
            {
                ViewBag.TotalGrossVolume = 0m;
                ViewBag.AdminCommission = 0m;
                ViewBag.InstructorPayouts = 0m;
                ViewBag.TotalOrders = 0;
                ViewBag.AllInvoices = new List<InvoiceViewModel>();
            }

            return View();
        }

        // GET: /Admin/VerifySellers?status=Pending
        [HttpGet]
        public ActionResult VerifySellers(string status = "Pending")
        {
            if (!IsAdmin())
            {
                TempData["WarningMessage"] = "Administrator access required. Please log in as admin.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("VerifySellers", "Admin") });
            }

            ViewBag.CurrentStatus = status ?? "Pending";
            var instructors = _instructorRepo.GetAllInstructorsWithProfiles(status).ToList();
            return View(instructors);
        }

        // GET: /Admin/VerifyInstructor/5
        [HttpGet]
        public ActionResult VerifyInstructor(int id)
        {
            if (!IsAdmin())
            {
                TempData["WarningMessage"] = "Administrator access required. Please log in as admin.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("VerifyInstructor", "Admin", new { id = id }) });
            }

            var profile = _instructorRepo.GetInstructorVerificationDetails(id);
            if (profile == null)
            {
                TempData["ErrorMessage"] = "Instructor application not found.";
                return RedirectToAction("VerifySellers");
            }

            return View(profile);
        }

        // POST: /Admin/ReviewInstructorWithFeedback
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewInstructorWithFeedback(ReviewInstructorViewModel model)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            if (model == null || model.UserId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid instructor application data.";
                return RedirectToAction("VerifySellers");
            }

            string action = (model.Action ?? "Approve").Trim();
            if (string.Equals(action, "RequestChanges", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(model.AdminFeedback))
            {
                TempData["ErrorMessage"] = "Please provide specific improvement instructions in the feedback notes so the instructor knows what to revise.";
                return RedirectToAction("VerifyInstructor", new { id = model.UserId });
            }

            int? adminId = GetCurrentAdminId();
            var result = _instructorRepo.ReviewInstructorWithFeedback(model.UserId, action, model.AdminFeedback, adminId);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("VerifyInstructor", new { id = model.UserId });
        }

        // POST: /Admin/ReviewInstructor (Legacy support)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReviewInstructor(int userId, string action)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Account");

            int? adminId = GetCurrentAdminId();
            var result = _instructorRepo.ReviewInstructorWithFeedback(userId, action, null, adminId);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Index");
        }
    }
}
