using System.Collections.Generic;

namespace WebApplication1.Models
{
    public class LessonItem
    {
        public int ModuleId { get; set; }
        public int LessonNumber { get; set; }
        public string Title { get; set; }
        public string Duration { get; set; }
        public bool IsCompleted { get; set; }
        public string Summary { get; set; }
        public string ContentMarkdown { get; set; }
        public string VideoUrl { get; set; }
        public string AssignmentPrompt { get; set; }
    }

    public class CoursePlayerViewModel
    {
        public Course Course { get; set; }
        public int CurrentProgress { get; set; }
        public string Status { get; set; }
        public List<LessonItem> Lessons { get; set; } = new List<LessonItem>();
        public LessonItem ActiveLesson { get; set; }
    }
}
