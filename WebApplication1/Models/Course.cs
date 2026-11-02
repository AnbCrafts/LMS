using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class Course
    {
        public int CourseId { get; set; }

        [Required(ErrorMessage = "Course Title is required.")]
        [StringLength(150, ErrorMessage = "Title cannot exceed 150 characters.")]
        [Display(Name = "Course Title")]
        public string Title { get; set; }

        public int? InstructorId { get; set; }

        [Required(ErrorMessage = "Instructor Name is required.")]
        [StringLength(100, ErrorMessage = "Instructor Name cannot exceed 100 characters.")]
        [Display(Name = "Instructor")]
        public string InstructorName { get; set; }

        [Display(Name = "Category")]
        public string Category { get; set; } = "Frontend"; // "Frontend", "Backend", "Fullstack", "DevOps"

        [Display(Name = "Price ($)")]
        public decimal Price { get; set; } = 49.99m;

        [Required(ErrorMessage = "Duration is required.")]
        [Range(1, 52, ErrorMessage = "Duration must be between 1 and 52 weeks.")]
        [Display(Name = "Duration (Weeks)")]
        public int DurationWeeks { get; set; } = 6;

        [Required(ErrorMessage = "Level is required.")]
        [StringLength(50)]
        [Display(Name = "Difficulty Level")]
        public string Level { get; set; } = "Beginner"; // "Beginner", "Intermediate", "Advanced"

        [Required(ErrorMessage = "Description is required.")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Total Enrolled")]
        public int TotalEnrolled { get; set; } = 0;
    }
}
