using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Repositories;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ICourseRepository _courseRepo;

        public HomeController() : this(new CourseRepository())
        {
        }

        public HomeController(ICourseRepository courseRepo)
        {
            _courseRepo = courseRepo;
        }

        public ActionResult Index()
        {
            var model = new LandingPageViewModel();

            try
            {
                // 1. Dynamic Featured Categories
                var dbCats = _courseRepo.GetCourseCategories().ToList();
                foreach (var cat in dbCats)
                {
                    model.Categories.Add(new LandingCategoryItem
                    {
                        CategoryName = cat.CategoryName,
                        CourseCount = cat.CourseCount,
                        Description = GetCategoryDescription(cat.CategoryName),
                        IconClass = GetCategoryIcon(cat.CategoryName)
                    });
                }

                // 2. Dynamic Featured Courses (Top priority ordered by enrollment / date)
                model.FeaturedCourses = _courseRepo.GetAllCourses()
                    .OrderByDescending(c => c.TotalEnrolled)
                    .ThenByDescending(c => c.CreatedAt)
                    .Take(4)
                    .ToList();

                // 3. Real Dynamic Platform Statistics
                string statsSql = @"
                    SELECT 
                        (SELECT COUNT(1) FROM LMS.Users WHERE Role = 'Student') AS ActiveLearners,
                        (SELECT COUNT(1) FROM LMS.Courses) AS AvailableCourses,
                        (SELECT COUNT(1) FROM LMS.Users WHERE Role = 'Instructor') AS ExpertInstructors,
                        (SELECT COUNT(1) FROM LMS.StudentSubmissions) + (SELECT COUNT(1) FROM LMS.StudentQuizAttempts) AS AssessmentsCompleted,
                        (SELECT COUNT(1) FROM LMS.StudentSubmissions WHERE Status = 'Approved') AS CertifiedGraduates;";

                DataTable dtStats = DbHelper.ExecuteDataTable(statsSql);
                if (dtStats != null && dtStats.Rows.Count > 0)
                {
                    var row = dtStats.Rows[0];
                    model.Stats.ActiveLearners = row["ActiveLearners"] != DBNull.Value ? Convert.ToInt32(row["ActiveLearners"]) : 0;
                    model.Stats.AvailableCourses = row["AvailableCourses"] != DBNull.Value ? Convert.ToInt32(row["AvailableCourses"]) : 0;
                    model.Stats.ExpertInstructors = row["ExpertInstructors"] != DBNull.Value ? Convert.ToInt32(row["ExpertInstructors"]) : 0;
                    model.Stats.AssessmentsCompleted = row["AssessmentsCompleted"] != DBNull.Value ? Convert.ToInt32(row["AssessmentsCompleted"]) : 0;
                    model.Stats.CertifiedGraduates = row["CertifiedGraduates"] != DBNull.Value ? Convert.ToInt32(row["CertifiedGraduates"]) : 0;
                }

                // 4. Real Dynamic Testimonials from LMS.Reviews
                string reviewsSql = @"
                    SELECT TOP 4 r.Rating, r.Comment, u.FullName AS StudentName, c.Title AS CourseTitle
                    FROM LMS.Reviews r
                    JOIN LMS.Users u ON r.UserId = u.UserId
                    JOIN LMS.Courses c ON r.CourseId = c.CourseId
                    ORDER BY r.CreatedAt DESC;";

                DataTable dtReviews = DbHelper.ExecuteDataTable(reviewsSql);
                if (dtReviews != null)
                {
                    foreach (DataRow r in dtReviews.Rows)
                    {
                        model.Testimonials.Add(new TestimonialItem
                        {
                            Rating = r["Rating"] != DBNull.Value ? Convert.ToInt32(r["Rating"]) : 5,
                            Comment = r["Comment"] != DBNull.Value ? r["Comment"].ToString() : string.Empty,
                            StudentName = r["StudentName"] != DBNull.Value ? r["StudentName"].ToString() : "Learner",
                            CourseTitle = r["CourseTitle"] != DBNull.Value ? r["CourseTitle"].ToString() : string.Empty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("HomeController.Index error: " + ex.Message);
            }

            // Compatibility support for views checking ViewBag
            ViewBag.TopCategories = _courseRepo.GetCourseCategories().Take(6).ToList();
            ViewBag.TotalCourses = model.Stats.AvailableCourses;

            return View(model);
        }

        public ActionResult About()
        {
            ViewBag.Message = "EduPulse — Enterprise Educational Ecosystem.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Contact EduPulse Academic & Institutional Support.";
            return View();
        }

        private string GetCategoryDescription(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return "Structured curriculums for practical skill acquisition.";
            string cat = categoryName.Trim().ToLowerInvariant();

            if (cat.Contains("fullstack")) return "End-to-end software engineering, modern architecture & production deployment.";
            if (cat.Contains("frontend")) return "Modern React, TypeScript, state systems & performant user interfaces.";
            if (cat.Contains("backend")) return "Scalable REST APIs, distributed microservices, C# .NET & data engines.";
            if (cat.Contains("cloud")) return "Cloud infrastructure patterns, serverless paradigms & high-availability designs.";
            if (cat.Contains("devops")) return "Continuous delivery pipelines, containerization, Docker & orchestration.";
            if (cat.Contains("data")) return "Statistical analysis, machine learning pipelines & data intelligence.";
            if (cat.Contains("design")) return "Product interface design systems, typography & human-centered UX.";
            if (cat.Contains("business")) return "Educational business operations, curriculum strategy & team leadership.";

            return "Master essential industry proficiencies with structured curriculum modules.";
        }

        private string GetCategoryIcon(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return "bi-journal-bookmark";
            string cat = categoryName.Trim().ToLowerInvariant();

            if (cat.Contains("fullstack")) return "bi-layers";
            if (cat.Contains("frontend")) return "bi-laptop";
            if (cat.Contains("backend")) return "bi-server";
            if (cat.Contains("cloud")) return "bi-cloud-check";
            if (cat.Contains("devops")) return "bi-cpu";
            if (cat.Contains("data")) return "bi-graph-up";
            if (cat.Contains("design")) return "bi-palette";
            if (cat.Contains("business")) return "bi-briefcase";

            return "bi-folder2-open";
        }
    }
}
