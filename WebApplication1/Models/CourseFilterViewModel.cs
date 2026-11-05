using System;
using System.Collections.Generic;
using System.Linq;

namespace WebApplication1.Models
{
    public class CategoryFilterItem
    {
        public string CategoryName { get; set; }
        public int CourseCount { get; set; }
        public bool IsSelected { get; set; }
    }

    public class CourseFilterViewModel
    {
        public string Search { get; set; } = string.Empty;
        public List<string> SelectedCategories { get; set; } = new List<string>();
        public string Level { get; set; } = "All"; // "All", "Beginner", "Intermediate", "Advanced"
        public string PriceFilter { get; set; } = "All"; // "All", "Free", "Paid", "Under50", "Over50"
        public string SortBy { get; set; } = "Popular"; // "Popular", "Newest", "PriceAsc", "PriceDesc", "TitleAsc"
        public string SelectedInstructor { get; set; } = "All";
        public string DurationFilter { get; set; } = "All"; // "All", "Short", "Medium", "Deep"
        public bool CertificateOnly { get; set; } = false;

        // Dynamic categories from DB
        public List<CategoryFilterItem> AvailableCategories { get; set; } = new List<CategoryFilterItem>();

        // Dynamic instructors from DB
        public List<string> AvailableInstructors { get; set; } = new List<string>();

        // Result courses matching filters
        public List<Course> Courses { get; set; } = new List<Course>();

        // Personalized recommendations if user has history
        public List<Course> RecommendedCourses { get; set; } = new List<Course>();

        // User's active enrollments for progress-awareness
        public Dictionary<int, StudentDashboardViewModel> UserEnrollments { get; set; } = new Dictionary<int, StudentDashboardViewModel>();

        public int TotalCatalogCount { get; set; }
        public int FilteredCount => Courses?.Count ?? 0;

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search) ||
            (SelectedCategories != null && SelectedCategories.Count > 0) ||
            (!string.IsNullOrWhiteSpace(Level) && Level != "All") ||
            (!string.IsNullOrWhiteSpace(PriceFilter) && PriceFilter != "All") ||
            (!string.IsNullOrWhiteSpace(SelectedInstructor) && SelectedInstructor != "All") ||
            (!string.IsNullOrWhiteSpace(DurationFilter) && DurationFilter != "All") ||
            CertificateOnly ||
            (!string.IsNullOrWhiteSpace(SortBy) && SortBy != "Popular");
    }
}
