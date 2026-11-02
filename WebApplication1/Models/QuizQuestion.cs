using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class QuizQuestion
    {
        public int QuestionId { get; set; }

        public int CourseId { get; set; }

        [Required]
        [Range(1, 10, ErrorMessage = "Lesson Number must be between 1 and 10.")]
        [Display(Name = "Lesson #")]
        public int LessonNumber { get; set; } = 1;

        [Required(ErrorMessage = "Question text is required.")]
        [StringLength(500)]
        [Display(Name = "Question")]
        public string QuestionText { get; set; }

        [Required(ErrorMessage = "Option A is required.")]
        [StringLength(250)]
        [Display(Name = "Option A")]
        public string OptionA { get; set; }

        [Required(ErrorMessage = "Option B is required.")]
        [StringLength(250)]
        [Display(Name = "Option B")]
        public string OptionB { get; set; }

        [Required(ErrorMessage = "Option C is required.")]
        [StringLength(250)]
        [Display(Name = "Option C")]
        public string OptionC { get; set; }

        [Required(ErrorMessage = "Option D is required.")]
        [StringLength(250)]
        [Display(Name = "Option D")]
        public string OptionD { get; set; }

        [Required(ErrorMessage = "Correct option is required.")]
        [RegularExpression("^[A-Da-d]$", ErrorMessage = "Correct option must be A, B, C, or D.")]
        [Display(Name = "Correct Answer (A, B, C, or D)")]
        public string CorrectOption { get; set; } = "A";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
