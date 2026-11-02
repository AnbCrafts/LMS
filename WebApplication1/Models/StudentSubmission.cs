using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class StudentSubmission
    {
        public int SubmissionId { get; set; }

        public int CourseId { get; set; }
        public string CourseTitle { get; set; }

        public int? ModuleId { get; set; }
        public string ModuleTitle { get; set; }
        public int? ModuleNumber { get; set; }

        public int UserId { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }

        [Display(Name = "Submission Title / Task")]
        public string SubmissionTitle { get; set; }

        [Display(Name = "Student Answer / Work Details")]
        public string SubmissionText { get; set; }

        [Display(Name = "Resource / Project Link")]
        public string SubmissionFileUrl { get; set; }

        [Display(Name = "Submitted On")]
        public DateTime SubmittedAt { get; set; } = DateTime.Now;

        [Display(Name = "Marks Awarded")]
        [Range(0, 100, ErrorMessage = "Marks must be between 0 and 100.")]
        public decimal? MarksObtained { get; set; }

        [Display(Name = "Total Marks")]
        public decimal MaxMarks { get; set; } = 100.00m;

        [Display(Name = "Instructor Feedback / Review Note")]
        [DataType(DataType.MultilineText)]
        public string InstructorFeedback { get; set; }

        [Display(Name = "Review Status")]
        public string Status { get; set; } = "Pending Review"; // "Pending Review", "Approved", "Rejected"

        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public string InstructorName { get; set; }

        public bool IsPending => Status == "Pending Review";
        public bool IsApproved => Status == "Approved";
    }

    public class GradeSubmissionViewModel
    {
        [Required]
        public int SubmissionId { get; set; }

        [Required(ErrorMessage = "Please award marks.")]
        [Range(0, 100, ErrorMessage = "Marks must be between 0 and 100.")]
        public decimal MarksObtained { get; set; }

        [Required(ErrorMessage = "Please provide feedback or review notes.")]
        [StringLength(1000)]
        public string InstructorFeedback { get; set; }

        [Required]
        public string Status { get; set; } = "Approved"; // "Approved" or "Rejected"
    }
}
