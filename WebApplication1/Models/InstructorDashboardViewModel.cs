using System;
using System.Collections.Generic;

namespace WebApplication1.Models
{
    public class InstructorStudentQueryItem
    {
        public int QueryLogId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public string StudentName { get; set; }
        public string QueryText { get; set; }
        public string Topic { get; set; }
        public string FrictionLevel { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class InstructorFrictionClusterItem
    {
        public string Topic { get; set; }
        public string FrictionLevel { get; set; }
        public int QueryCount { get; set; }
    }

    public class InstructorActivityItem
    {
        public string Type { get; set; } // "enrollment", "submission", "quiz"
        public string StudentName { get; set; }
        public string CourseTitle { get; set; }
        public string Detail { get; set; }
        public DateTime Timestamp { get; set; }
        public string BadgeClass { get; set; }
    }

    public class InstructorScheduleItem
    {
        public string Title { get; set; }
        public string CourseTitle { get; set; }
        public DateTime ScheduledTime { get; set; }
        public string Duration { get; set; }
        public string Platform { get; set; }
        public bool IsToday { get; set; }
    }
}
