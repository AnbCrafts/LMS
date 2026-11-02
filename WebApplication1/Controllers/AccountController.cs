using System;
using System.Web.Mvc;
using WebApplication1.Models;
using WebApplication1.Repositories;

namespace WebApplication1.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserRepository _userRepo;

        public AccountController()
        {
            _userRepo = new UserRepository();
        }

        public AccountController(IUserRepository userRepo)
        {
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

        // GET: /Account/ApplicationStatus
        [HttpGet]
        public ActionResult ApplicationStatus(string email = null)
        {
            return RedirectToAction("ApplicationStatus", "Instructor", new { email = email });
        }

        // GET: /Account/Login
        [HttpGet]
        public ActionResult Login(string returnUrl = null)
        {
            // If already logged in, redirect
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            var model = new LoginViewModel { ReturnUrl = returnUrl };
            return View(model);
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = _userRepo.Authenticate(model.Email, model.Password);

                if (user == null)
                {
                    ModelState.AddModelError("", "Invalid email address or password.");
                    return View(model);
                }

                // Strict instructor verification gating: prevent rejected instructors from logging in
                if (string.Equals(user.Role, "Instructor", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(user.ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("", "Your instructor application has been rejected by the administrator. Contact support or submit a revised application.");
                        return View(model);
                    }
                }

                // Set user session variables
                Session["CurrentUser"] = user;
                Session["UserId"] = user.UserId;
                Session["UserName"] = user.FullName;
                Session["UserEmail"] = user.Email;
                Session["UserRole"] = user.Role;
                Session["IsApproved"] = user.IsApproved;
                Session["ApprovalStatus"] = user.ApprovalStatus;

                TempData["SuccessMessage"] = $"Welcome back, {user.FullName}!";

                if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                if (user.Role == "Admin")
                {
                    return RedirectToAction("Index", "Admin");
                }

                if (user.Role == "Instructor")
                {
                    if (!user.IsApproved)
                    {
                        TempData["WarningMessage"] = string.Equals(user.ApprovalStatus, "Action Required", StringComparison.OrdinalIgnoreCase)
                            ? "Action Required: The administrator reviewed your application and requested revisions. Please review the feedback below."
                            : "Your instructor application is pending verification. Please make sure your profile is at least 90% complete.";
                        return RedirectToAction("Profile", "Instructor");
                    }
                    return RedirectToAction("Index", "Instructor");
                }

                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An unexpected error occurred during login: " + ex.Message);
                return View(model);
            }
        }

        // POST: /Account/QuickLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult QuickLogin(string persona, string returnUrl = null)
        {
            string email;
            string password;

            switch (persona)
            {
                case "Admin":
                    email = "admin@lms.com";
                    password = "Admin@123";
                    break;
                case "Instructor":
                    email = "asharma@lms.com";
                    password = "Pass@123";
                    break;
                case "PendingInstructor":
                    email = "pmehta@lms.com";
                    password = "Pass@123";
                    break;
                case "Student":
                default:
                    email = "student@lms.com";
                    password = "Pass@123";
                    break;
            }

            var user = _userRepo.Authenticate(email, password);
            if (user != null)
            {
                // Strict instructor verification gating: prevent rejected instructors
                if (string.Equals(user.Role, "Instructor", StringComparison.OrdinalIgnoreCase) && string.Equals(user.ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Access Denied: Your instructor application has been rejected by the administrator.";
                    return RedirectToAction("Login");
                }

                Session["CurrentUser"] = user;
                Session["UserId"] = user.UserId;
                Session["UserName"] = user.FullName;
                Session["UserEmail"] = user.Email;
                Session["UserRole"] = user.Role;
                Session["IsApproved"] = user.IsApproved;
                Session["ApprovalStatus"] = user.ApprovalStatus;

                TempData["SuccessMessage"] = $"Switched persona to: {user.FullName} ({user.Role})";

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                if (user.Role == "Admin") return RedirectToAction("Index", "Admin");
                if (user.Role == "Instructor")
                {
                    if (!user.IsApproved) return RedirectToAction("Profile", "Instructor");
                    return RedirectToAction("Index", "Instructor");
                }
                return RedirectToAction("Index", "Dashboard");
            }

            TempData["ErrorMessage"] = $"Demo account '{email}' was not found in the database. Ensure LMS.sql sample data was seeded.";
            return RedirectToAction("Login");
        }

        // GET: /Account/Register
        [HttpGet]
        public ActionResult Register()
        {
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View(new RegisterViewModel());
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var newUser = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Password = model.Password,
                    Role = model.Role
                };

                var createdUser = _userRepo.Register(newUser);

                if (createdUser == null)
                {
                    ModelState.AddModelError("", "Failed to register account. Please try again.");
                    return View(model);
                }

                // If Student: Auto-approved, log in immediately
                if (createdUser.Role == "Student")
                {
                    Session["CurrentUser"] = createdUser;
                    Session["UserId"] = createdUser.UserId;
                    Session["UserName"] = createdUser.FullName;
                    Session["UserEmail"] = createdUser.Email;
                    Session["UserRole"] = createdUser.Role;
                    Session["IsApproved"] = true;
                    Session["ApprovalStatus"] = "Approved";

                    TempData["SuccessMessage"] = "Account created successfully! Welcome to EduPulse LMS.";
                    return RedirectToAction("Index", "Courses");
                }
                else
                {
                    // If Instructor: Account is pending admin verification
                    TempData["SuccessMessage"] = "Registration successful! As an instructor, your account is awaiting administrator review and verification. You cannot log in until approved by an administrator.";
                    return RedirectToAction("Login");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        // GET: /Account/Profile
        [HttpGet]
        public new ActionResult Profile()
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login");
            }

            // Prefer user from active session, fallback to database
            var user = Session["CurrentUser"] as User;
            if (user == null || user.UserId != userId.Value)
            {
                try
                {
                    user = _userRepo.GetUserById(userId.Value);
                }
                catch
                {
                    user = new User
                    {
                        UserId = userId.Value,
                        FullName = Session["UserName"] as string ?? "User",
                        Email = Session["UserEmail"] as string ?? "user@lms.com",
                        Role = Session["UserRole"] as string ?? "Student",
                        IsApproved = Session["IsApproved"] != null && Convert.ToBoolean(Session["IsApproved"]),
                        ApprovalStatus = Session["ApprovalStatus"] as string ?? "Approved"
                    };
                }
            }

            if (user == null)
            {
                return HttpNotFound();
            }

            ViewBag.ChangePasswordModel = new ChangePasswordViewModel();
            return View(user);
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            int? userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                var user = Session["CurrentUser"] as User;
                ViewBag.ChangePasswordModel = model;
                return View("Profile", user);
            }

            var result = _userRepo.ChangePassword(userId.Value, model.CurrentPassword, model.NewPassword);
            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = "Password updated successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Profile");
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }
    }
}
