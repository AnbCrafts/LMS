using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class Enrollment
    {
        public int EnrollmentId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Range(0, 100, ErrorMessage = "Progress must be between 0 and 100.")]
        [Display(Name = "Progress (%)")]
        public int ProgressPercentage { get; set; } = 0;

        [StringLength(30)]
        [Display(Name = "Status")]
        public string Status { get; set; } = "In Progress"; // "In Progress", "Completed"

        [Display(Name = "Enrolled Date")]
        public DateTime EnrolledAt { get; set; } = DateTime.Now;

        // Navigation helpers
        public Course Course { get; set; }
        public User User { get; set; }
    }
}
