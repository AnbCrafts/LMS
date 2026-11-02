using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class CourseModule
    {
        public int ModuleId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required(ErrorMessage = "Module order is required.")]
        [Display(Name = "Module #")]
        public int ModuleNumber { get; set; } = 1;

        [Required(ErrorMessage = "Module title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        [Display(Name = "Module Title")]
        public string Title { get; set; }

        [DataType(DataType.MultilineText)]
        [Display(Name = "Short Overview / Summary")]
        public string Description { get; set; }

        [Required]
        [Range(5, 600, ErrorMessage = "Duration must be between 5 and 600 minutes.")]
        [Display(Name = "Duration (Minutes)")]
        public int DurationMinutes { get; set; } = 30;

        [DataType(DataType.MultilineText)]
        [Display(Name = "Module Curriculum & Content (Markdown)")]
        public string ContentMarkdown { get; set; }

        [Display(Name = "Video or Resource Embed URL")]
        [StringLength(500)]
        public string VideoOrResourceUrl { get; set; }

        [DataType(DataType.MultilineText)]
        [Display(Name = "Hands-on Assignment Task / Prompt")]
        public string AssignmentPrompt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Display helper
        public string FormattedDuration => $"{DurationMinutes} mins";
    }
}
