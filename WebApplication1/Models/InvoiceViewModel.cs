using System;

namespace WebApplication1.Models
{
    public class InvoiceViewModel
    {
        public int PaymentId { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionId { get; set; }
        public string Status { get; set; }
        public DateTime PaymentDate { get; set; }

        // Financial Split: 10% Admin Platform Charges, 90% Course Seller (Instructor) Net
        public decimal PlatformFee => Math.Round(Amount * 0.10m, 2);
        public decimal InstructorShare => Amount - PlatformFee;

        // Student Info
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }

        // Course Info
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public string Category { get; set; }
        public string Level { get; set; }
        public int DurationWeeks { get; set; }

        // Instructor Info
        public int? InstructorId { get; set; }
        public string InstructorName { get; set; }
        public string InstructorEmail { get; set; }
    }
}
