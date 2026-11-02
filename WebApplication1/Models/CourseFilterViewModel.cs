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

        // Dynamic categories from DB
        public List<CategoryFilterItem> AvailableCategories { get; set; } = new List<CategoryFilterItem>();

        // Result courses matching filters
        public List<Course> Courses { get; set; } = new List<Course>();

        public int TotalCatalogCount { get; set; }
        public int FilteredCount => Courses?.Count ?? 0;

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Search) ||
            (SelectedCategories != null && SelectedCategories.Count > 0) ||
            (!string.IsNullOrWhiteSpace(Level) && Level != "All") ||
            (!string.IsNullOrWhiteSpace(PriceFilter) && PriceFilter != "All") ||
            (!string.IsNullOrWhiteSpace(SortBy) && SortBy != "Popular");
    }
}
