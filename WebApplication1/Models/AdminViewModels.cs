using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebApplication1.Data;

namespace WebApplication1.Models
{
    // ==========================================================================
    // EduPulse Admin Operations Ecosystem ViewModels
    // Inspired by Datadog, Stripe Dashboard, Cloudflare, Linear, GitHub Admin
    // ==========================================================================

    #region Admin Navbar ViewModel

    public class AdminNavbarViewModel
    {
        public string AdminName { get; set; } = "Operations Administrator";
        public string Email { get; set; } = "admin@lms.com";
        public string AvatarInitials { get; set; } = "AD";
        public string SystemStatus { get; set; } = "Operational";
        public string SlaUptime { get; set; } = "99.98%";
        public int AiLatencyMs { get; set; } = 142;
        public string FastApiRagStatus { get; set; } = "Healthy (Port 8000)";
        public int PendingReviewsCount { get; set; } = 4;
        public int OpenTicketsCount { get; set; } = 3;
        public int FlaggedReportsCount { get; set; } = 2;
        public List<AdminSearchItem> SearchItems { get; set; } = new List<AdminSearchItem>();
        public List<AdminNotificationItem> Notifications { get; set; } = new List<AdminNotificationItem>();

        public static AdminNavbarViewModel LoadForUser(User user)
        {
            var vm = new AdminNavbarViewModel();
            if (user != null)
            {
                vm.AdminName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : "Operations Administrator";
                vm.Email = user.Email ?? "admin@lms.com";

                var parts = vm.AdminName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    vm.AvatarInitials = $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpper();
                else if (parts.Length == 1 && parts[0].Length > 0)
                    vm.AvatarInitials = parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            }

            // Populate global administrative search catalog items
            vm.SearchItems = new List<AdminSearchItem>
            {
                new AdminSearchItem { Title = "User Management Directory", Category = "Users", TargetUrl = "/Admin?tab=users", Subtitle = "Manage 1,280 accounts, roles & status" },
                new AdminSearchItem { Title = "Instructor Verification Queue", Category = "Instructors", TargetUrl = "/Admin?tab=instructors", Subtitle = "Pending teacher credentials & pedigree audits" },
                new AdminSearchItem { Title = "Course Moderation Catalog", Category = "Courses", TargetUrl = "/Admin?tab=courses", Subtitle = "Approve, publish & suspend platform curriculums" },
                new AdminSearchItem { Title = "AI Governance Center", Category = "AI Ops", TargetUrl = "/Admin?tab=ai-governance", Subtitle = "Audit RAG queries, prompt injection & latency SLA" },
                new AdminSearchItem { Title = "Financial Ledger & 10% Cuts", Category = "Finance", TargetUrl = "/Admin?tab=finance", Subtitle = "Gross sales, Stripe settlement & seller payouts" },
                new AdminSearchItem { Title = "Platform Analytics & BI Hub", Category = "Analytics", TargetUrl = "/Admin?tab=analytics", Subtitle = "Enrollment velocity, cohort retention & metrics" },
                new AdminSearchItem { Title = "Certificates Administration", Category = "Certificates", TargetUrl = "/Admin?tab=certificates", Subtitle = "Validate authentic hashes & revoke fraud passes" },
                new AdminSearchItem { Title = "Reports & Incident Management", Category = "Reports", TargetUrl = "/Admin?tab=reports", Subtitle = "Investigate flagged courses, grievances & copyright" },
                new AdminSearchItem { Title = "Customer Operations Helpdesk", Category = "Support", TargetUrl = "/Admin?tab=support", Subtitle = "Resolve student & teacher SLA escalation tickets" },
                new AdminSearchItem { Title = "Platform Configuration Settings", Category = "Settings", TargetUrl = "/Admin?tab=settings", Subtitle = "Security policies, API keys, gateways & rules" },
                new AdminSearchItem { Title = "A. Sharma (Staff Instructor)", Category = "Users", TargetUrl = "/Admin?tab=users&q=asharma", Subtitle = "asharma@lms.com • 3 Published Courses" },
                new AdminSearchItem { Title = "Rahul Verma (Student)", Category = "Users", TargetUrl = "/Admin?tab=users&q=student", Subtitle = "student@lms.com • 2 Active Enrollments" },
                new AdminSearchItem { Title = "Modern React 18 Masterclass", Category = "Courses", TargetUrl = "/Admin?tab=courses&q=react", Subtitle = "Course #1 • 4 Modules • 100% Quality Score" }
            };

            // System notifications
            vm.Notifications = new List<AdminNotificationItem>
            {
                new AdminNotificationItem { Title = "Instructor Application Submitted", Message = "A. Sharma submitted pedagogy credentials awaiting verification.", IsUrgent = true, TargetUrl = "/Admin?tab=instructors", TimestampFormatted = "10 mins ago" },
                new AdminNotificationItem { Title = "AI Prompt Guard Triggered", Message = "Prohibited system prompt extraction query blocked by safety guardrails.", IsUrgent = true, TargetUrl = "/Admin?tab=ai-governance", TimestampFormatted = "28 mins ago" },
                new AdminNotificationItem { Title = "Weekly Payout Batch Ready", Message = "Gross volume $1,280.00 settled. Instructor payout disbursements prepared.", IsUrgent = false, TargetUrl = "/Admin?tab=finance", TimestampFormatted = "1 hr ago" },
                new AdminNotificationItem { Title = "Course Published Successfully", Message = "Enterprise ASP.NET MVC architecture approved and published.", IsUrgent = false, TargetUrl = "/Admin?tab=courses", TimestampFormatted = "3 hrs ago" }
            };

            return vm;
        }
    }

    public class AdminSearchItem
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public string TargetUrl { get; set; }
        public string Subtitle { get; set; }
    }

    public class AdminNotificationItem
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsUrgent { get; set; }
        public string TargetUrl { get; set; }
        public string TimestampFormatted { get; set; }
    }

    #endregion

    #region Admin Dashboard ViewModel

    public class AdminDashboardViewModel
    {
        // 1. Overview KPIs
        public int TotalUsers { get; set; } = 1284;
        public int ActiveLearners { get; set; } = 948;
        public int ActiveInstructors { get; set; } = 38;
        public int PublishedCourses { get; set; } = 24;
        public int DailyEnrollments { get; set; } = 47;
        public int CertificatesIssued { get; set; } = 186;
        public int AiConversations { get; set; } = 3412;
        public double PlatformEngagementScore { get; set; } = 94.8;

        // Financial summary
        public decimal GrossRevenue { get; set; } = 18450.00m;
        public decimal AdminPlatformCut10Pct { get; set; } = 1845.00m;
        public decimal InstructorPayouts90Pct { get; set; } = 16605.00m;

        // System SLAs
        public string FastApiRagStatus { get; set; } = "Operational (Port 8000)";
        public string VectorStoreStatus { get; set; } = "Indexed (ChromaDB)";
        public double SystemUptimePercent { get; set; } = 99.98;
        public int AiResponseLatencyMs { get; set; } = 142;

        // 2. Platform Activity Center
        public List<AdminInstructorItem> PendingInstructorApplications { get; set; } = new List<AdminInstructorItem>();
        public List<AdminCourseItem> CoursesPendingModeration { get; set; } = new List<AdminCourseItem>();
        public List<AdminReportItem> ActiveReports { get; set; } = new List<AdminReportItem>();
        public List<AdminSupportItem> UrgentSupportTickets { get; set; } = new List<AdminSupportItem>();
        public List<AdminAiGovernanceItem> RecentFlaggedAiQueries { get; set; } = new List<AdminAiGovernanceItem>();
        public List<AdminAuditLogItem> RecentAuditEvents { get; set; } = new List<AdminAuditLogItem>();

        // Navigation state
        public string ActiveTab { get; set; } = "dashboard";
    }

    #endregion

    #region Module Specific Models

    public class AdminUserItem
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsApproved { get; set; }
        public string ApprovalStatus { get; set; }
        public string AccountStatus { get; set; } = "Active"; // Active, Suspended, Deactivated
        public DateTime RegisteredAt { get; set; }
        public int EnrolledCoursesCount { get; set; }
        public int CompletedCertificatesCount { get; set; }
        public string LastActiveFormatted { get; set; }
        public string AvatarInitials => !string.IsNullOrEmpty(FullName) ? string.Join("", FullName.Split(' ').Where(p => p.Length > 0).Select(p => p[0])).ToUpper() : "U";
    }

    public class AdminInstructorItem
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string PrimaryExpertise { get; set; }
        public string CurrentOrganization { get; set; }
        public int TotalExperienceYears { get; set; }
        public string Bio { get; set; }
        public string HighestDegree { get; set; }
        public string InstitutionName { get; set; }
        public string LinkedInUrl { get; set; }
        public string GitHubUrl { get; set; }
        public string ApprovalStatus { get; set; } // Pending, Approved, Rejected, ChangesRequested
        public int ActiveCoursesCount { get; set; }
        public int StudentsTaughtCount { get; set; }
        public double CompletionRate { get; set; } = 89.4;
        public double AverageRating { get; set; } = 4.9;
        public decimal TotalEarnings { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string AdminFeedback { get; set; }
        public string AvatarInitials => !string.IsNullOrEmpty(FullName) ? string.Join("", FullName.Split(' ').Where(p => p.Length > 0).Select(p => p[0])).ToUpper() : "IN";
    }

    public class AdminCourseItem
    {
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string InstructorName { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; } = "Published"; // Published, Pending, Draft, Suspended, Archived
        public int ModuleCount { get; set; }
        public int EnrolledCount { get; set; }
        public double Rating { get; set; } = 4.8;
        public int ReportedCount { get; set; } = 0;
        public double QualityScore { get; set; } = 96.5;
        public DateTime CreatedAt { get; set; }
    }

    public class AdminAiGovernanceItem
    {
        public int IncidentId { get; set; }
        public string UserName { get; set; }
        public string Role { get; set; }
        public string CourseContext { get; set; }
        public string PromptExcerpt { get; set; }
        public string TriggerReason { get; set; } // Prompt Injection, Exam Solution Leak Attempt, Hate Speech, PII Extraction
        public string Severity { get; set; } = "High"; // Critical, High, Medium, Low
        public string ActionTaken { get; set; } = "Blocked by Safety Shield";
        public DateTime Timestamp { get; set; }
        public int LatencyMs { get; set; } = 138;
        public int TokenUsage { get; set; } = 245;
    }

    public class AdminReportItem
    {
        public int ReportId { get; set; }
        public string Category { get; set; } // Inappropriate Content, Copyright Violation, Learner Grievance, AI Misuse, Instructor Dispute
        public string Title { get; set; }
        public string ReportedItem { get; set; }
        public string ReportedBy { get; set; }
        public string Severity { get; set; } = "High"; // Critical, High, Medium, Low
        public string Status { get; set; } = "Open"; // Open, In Investigation, Resolved, Dismissed
        public string AssignedTo { get; set; } = "Compliance Lead";
        public DateTime CreatedAt { get; set; }
        public string ResolutionNotes { get; set; }
    }

    public class AdminSupportItem
    {
        public int TicketId { get; set; }
        public string Subject { get; set; }
        public string RequesterName { get; set; }
        public string RequesterRole { get; set; } = "Student";
        public string Category { get; set; } = "Technical Issue";
        public string Priority { get; set; } = "P2 - High"; // P1 - Critical, P2 - High, P3 - Normal, P4 - Low
        public string Status { get; set; } = "Open"; // Open, In Progress, Resolved
        public int SlaRemainingMinutes { get; set; } = 45;
        public string AssignedAgent { get; set; } = "Support Team";
        public DateTime CreatedAt { get; set; }
        public string LastMessage { get; set; }
    }

    public class AdminCertificateItem
    {
        public int CertificateId { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }
        public string CourseTitle { get; set; }
        public string InstructorName { get; set; }
        public string VerificationCode { get; set; }
        public double FinalScore { get; set; }
        public string Status { get; set; } = "Valid"; // Valid, Revoked, Under Investigation
        public DateTime IssuedAt { get; set; }
    }

    public class AdminAuditLogItem
    {
        public int LogId { get; set; }
        public string AdminUser { get; set; } = "Super Admin";
        public string ActionType { get; set; }
        public string TargetEntity { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; } = "127.0.0.1";
        public DateTime Timestamp { get; set; }
    }

    public class AdminSettingsViewModel
    {
        // General
        public string PlatformName { get; set; } = "EduPulse Academic Workspace";
        public string SupportEmail { get; set; } = "support@edupulse.com";
        public string ContactPhone { get; set; } = "+1 (800) 555-0199";
        public string PlatformTimeZone { get; set; } = "UTC-05:00 (Eastern Time)";
        public bool MaintenanceMode { get; set; } = false;

        // Branding
        public string BrandColorHex { get; set; } = "#0f172a";
        public string AccentColorHex { get; set; } = "#2563eb";
        public string LogoUrl { get; set; } = "/Content/logo.svg";

        // AI Assistant Governance Configuration
        public string PrimaryGeminiModel { get; set; } = "gemini-1.5-flash";
        public string SecondaryFallbackModel { get; set; } = "gemini-1.5-pro";
        public double ModelTemperature { get; set; } = 0.2;
        public int MaxTokensPerQuery { get; set; } = 1024;
        public bool StrictAcademicSafetyShield { get; set; } = true;
        public bool PreventExamAnswersLeak { get; set; } = true;
        public int UserRateLimitPerMinute { get; set; } = 20;

        // Platform Approval & Quality Policies
        public bool RequireAdminApprovalForInstructors { get; set; } = true;
        public bool RequireAdminReviewForCourses { get; set; } = true;
        public int MinimumInstructorExperienceYears { get; set; } = 2;
        public decimal PlatformRevenueFeePercentage { get; set; } = 10.0m;

        // Security & Authentication
        public bool EnforceTwoFactorAuthentication { get; set; } = false;
        public int SessionTimeoutMinutes { get; set; } = 60;
        public int MaxLoginAttemptsBeforeLock { get; set; } = 5;
        public bool RequireCsrfTokensOnAllMutations { get; set; } = true;

        // Email & Notification Gateways
        public string SmtpHost { get; set; } = "smtp.sendgrid.net";
        public int SmtpPort { get; set; } = 587;
        public string SmtpSenderEmail { get; set; } = "notifications@edupulse.com";

        // Financial & Payment Integration
        public string PaymentGatewayMode { get; set; } = "Test (Mock Mode)";
        public string StripePublishableKey { get; set; } = "pk_test_sample_51O9yZ2...";
        public string CurrencyCode { get; set; } = "USD ($)";
    }

    #endregion
}
