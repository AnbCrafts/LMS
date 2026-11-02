using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
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
            ViewBag.TopCategories = _courseRepo.GetCourseCategories().Take(6).ToList();
            ViewBag.TotalCourses = _courseRepo.GetAllCourses().Count();
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }
    }
}
