using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class StudentDashboardViewModel
    {
        public int EnrollmentId { get; set; }
        public int UserId { get; set; }
        public int CourseId { get; set; }

        [Display(Name = "Course")]
        public string Course { get; set; }

        [Display(Name = "Instructor")]
        public string InstructorName { get; set; }

        [Display(Name = "Duration")]
        public int DurationWeeks { get; set; }

        [Display(Name = "Level")]
        public string Level { get; set; }

        [Display(Name = "Progress")]
        public int Progress { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; }

        [Display(Name = "Enrolled Date")]
        public DateTime EnrolledAt { get; set; }

        // Gradebook & Performance Details
        public CourseGradeSummary GradeSummary { get; set; } = new CourseGradeSummary();
        public System.Collections.Generic.List<StudentSubmission> Submissions { get; set; } = new System.Collections.Generic.List<StudentSubmission>();
        public System.Collections.Generic.List<StudentQuizAttempt> QuizAttempts { get; set; } = new System.Collections.Generic.List<StudentQuizAttempt>();
    }
}
