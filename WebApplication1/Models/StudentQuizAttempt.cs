using System;

namespace WebApplication1.Models
{
    public class StudentQuizAttempt
    {
        public int AttemptId { get; set; }
        public int UserId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public int LessonNumber { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public decimal ScorePercentage { get; set; }
        public bool IsPassed { get; set; }
        public DateTime AttemptedAt { get; set; } = DateTime.Now;

        public string ScoreFormatted => $"{CorrectAnswers} / {TotalQuestions} ({ScorePercentage:0.0}%)";
    }

    public class CourseGradeSummary
    {
        public int UserId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }

        public decimal QuizAverage { get; set; }
        public int QuizzesTaken { get; set; }
        public int QuizzesPassed { get; set; }

        public decimal AssignmentAverage { get; set; }
        public int AssignmentsSubmitted { get; set; }
        public int AssignmentsApproved { get; set; }
        public int AssignmentsPending { get; set; }

        public decimal FinalScore { get; set; }
        public string LetterGrade { get; set; } = "Incomplete";

        public bool HasPendingAssignments => AssignmentsPending > 0;
        public bool IsDistinction => FinalScore >= 90;
        public bool IsPassing => FinalScore >= 60;
    }
}
