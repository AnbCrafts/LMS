using System.Collections.Generic;

namespace WebApplication1.Models
{
    public class LandingCategoryItem
    {
        public string CategoryName { get; set; }
        public int CourseCount { get; set; }
        public string Description { get; set; }
        public string IconClass { get; set; }
    }

    public class PlatformStatsModel
    {
        public int ActiveLearners { get; set; }
        public int AvailableCourses { get; set; }
        public int ExpertInstructors { get; set; }
        public int AssessmentsCompleted { get; set; }
        public int CertifiedGraduates { get; set; }
    }

    public class TestimonialItem
    {
        public string StudentName { get; set; }
        public string CourseTitle { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
    }

    public class LandingPageViewModel
    {
        public List<LandingCategoryItem> Categories { get; set; } = new List<LandingCategoryItem>();
        public List<Course> FeaturedCourses { get; set; } = new List<Course>();
        public PlatformStatsModel Stats { get; set; } = new PlatformStatsModel();
        public List<TestimonialItem> Testimonials { get; set; } = new List<TestimonialItem>();
    }
}
