using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class InstructorProfile
    {
        public int ProfileId { get; set; }
        public int UserId { get; set; }

        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public bool IsApproved { get; set; }
        public string ApprovalStatus { get; set; }

        // Category A: Personal & Contact
        [Display(Name = "Contact Phone")]
        [StringLength(50)]
        public string PhoneNumber { get; set; }

        [Display(Name = "Professional Headline")]
        [StringLength(200)]
        public string Headline { get; set; } // e.g. "Staff Cloud Architect & Senior Technical Trainer"

        [Display(Name = "City")]
        [StringLength(100)]
        public string City { get; set; }

        [Display(Name = "Country")]
        [StringLength(100)]
        public string Country { get; set; }

        // Category B: Professional & Teaching Experience
        [Display(Name = "Current Job Title")]
        [StringLength(150)]
        public string CurrentJobTitle { get; set; }

        [Display(Name = "Current Organization / Company")]
        [StringLength(150)]
        public string CurrentOrganization { get; set; }

        [Display(Name = "Total Experience (Years)")]
        [Range(0, 50, ErrorMessage = "Years must be between 0 and 50.")]
        public int? TotalExperienceYears { get; set; }

        [Display(Name = "Past Teaching Experience")]
        [DataType(DataType.MultilineText)]
        public string PastTeachingExperience { get; set; }

        [Display(Name = "Primary Domain Expertise")]
        [StringLength(250)]
        public string PrimaryExpertise { get; set; }

        [Display(Name = "Full Professional Bio")]
        [DataType(DataType.MultilineText)]
        public string Bio { get; set; }

        // Category C: Education & Qualifications
        [Display(Name = "Highest Degree / Qualification")]
        [StringLength(100)]
        public string HighestDegree { get; set; }

        [Display(Name = "Institution / University")]
        [StringLength(200)]
        public string InstitutionName { get; set; }

        [Display(Name = "Graduation Year")]
        [Range(1950, 2030, ErrorMessage = "Enter a valid graduation year.")]
        public int? GraduationYear { get; set; }

        // Category D: Course Offerings & Pricing Intent
        [Display(Name = "Courses You Intend to Teach")]
        [DataType(DataType.MultilineText)]
        public string PlannedCourses { get; set; }

        [Display(Name = "Target Student Level")]
        public string TargetLevel { get; set; } = "Beginner"; // "Beginner", "Intermediate", "Advanced"

        [Display(Name = "Proposed Course Price ($)")]
        public decimal? ProposedPricing { get; set; } = 49.99m;

        // Category E: Verification, Signature & Socials
        [Display(Name = "Profile Photo (Cloudinary)")]
        public string ProfilePictureUrl { get; set; }

        [Display(Name = "Digital Signature (For Certificates)")]
        public string DigitalSignatureUrl { get; set; }

        [Display(Name = "LinkedIn Profile URL")]
        [Url(ErrorMessage = "Please enter a valid URL.")]
        public string LinkedInUrl { get; set; }

        [Display(Name = "GitHub or Portfolio URL")]
        [Url(ErrorMessage = "Please enter a valid URL.")]
        public string GitHubOrPortfolioUrl { get; set; }

        [Display(Name = "Profile Completion (%)")]
        public int CompletionPercentage { get; set; } = 0;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Admin Feedback / Review Notes")]
        public string AdminFeedback { get; set; }

        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public string ReviewerName { get; set; }
        public DateTime RegisteredAt { get; set; } = DateTime.Now;
        public int PublishedCoursesCount { get; set; } = 0;

        public bool IsActionRequired => string.Equals(ApprovalStatus, "Action Required", StringComparison.OrdinalIgnoreCase);
        public bool IsPendingReview => !IsApproved && (string.IsNullOrWhiteSpace(ApprovalStatus) || string.Equals(ApprovalStatus, "Pending", StringComparison.OrdinalIgnoreCase));
        public bool IsApprovedStatus => IsApproved || string.Equals(ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase);
        public bool IsRejectedStatus => string.Equals(ApprovalStatus, "Rejected", StringComparison.OrdinalIgnoreCase);

        // Helper: Check if unlocked to create/upload courses
        public bool CanUploadCourse => CompletionPercentage >= 90 && IsApprovedStatus;

        // Checklist of missing items for the UI
        public List<ProfileCheckItem> GetChecklist()
        {
            var list = new List<ProfileCheckItem>
            {
                new ProfileCheckItem { Label = "Professional Headline", Weight = 10, IsCompleted = !string.IsNullOrWhiteSpace(Headline) && Headline.Trim().Length > 3 },
                new ProfileCheckItem { Label = "Phone & Location Details", Weight = 10, IsCompleted = !string.IsNullOrWhiteSpace(PhoneNumber) && (!string.IsNullOrWhiteSpace(City) || !string.IsNullOrWhiteSpace(Country)) },
                new ProfileCheckItem { Label = "Current Job Title & Company", Weight = 5, IsCompleted = !string.IsNullOrWhiteSpace(CurrentJobTitle) },
                new ProfileCheckItem { Label = "Total Industry Experience", Weight = 5, IsCompleted = TotalExperienceYears.HasValue && TotalExperienceYears.Value > 0 },
                new ProfileCheckItem { Label = "Past Teaching Experience", Weight = 5, IsCompleted = !string.IsNullOrWhiteSpace(PastTeachingExperience) && PastTeachingExperience.Trim().Length > 10 },
                new ProfileCheckItem { Label = "Primary Domain Expertise", Weight = 5, IsCompleted = !string.IsNullOrWhiteSpace(PrimaryExpertise) },
                new ProfileCheckItem { Label = "Comprehensive Professional Bio", Weight = 5, IsCompleted = !string.IsNullOrWhiteSpace(Bio) && Bio.Trim().Length >= 30 },
                new ProfileCheckItem { Label = "Highest Degree / Qualification", Weight = 10, IsCompleted = !string.IsNullOrWhiteSpace(HighestDegree) },
                new ProfileCheckItem { Label = "University / College Details", Weight = 10, IsCompleted = !string.IsNullOrWhiteSpace(InstitutionName) && GraduationYear.HasValue },
                new ProfileCheckItem { Label = "Course Offerings & Pricing Plan", Weight = 15, IsCompleted = !string.IsNullOrWhiteSpace(PlannedCourses) && ProposedPricing.HasValue && ProposedPricing.Value > 0 },
                new ProfileCheckItem { Label = "Profile Avatar (Cloudinary)", Weight = 5, IsCompleted = !string.IsNullOrWhiteSpace(ProfilePictureUrl) },
                new ProfileCheckItem { Label = "Digital Signature for Certificates (Cloudinary)", Weight = 10, IsCompleted = !string.IsNullOrWhiteSpace(DigitalSignatureUrl) },
                new ProfileCheckItem { Label = "LinkedIn or Portfolio Profile", Weight = 5, IsCompleted = !string.IsNullOrWhiteSpace(LinkedInUrl) || !string.IsNullOrWhiteSpace(GitHubOrPortfolioUrl) }
            };
            return list;
        }
    }

    public class ProfileCheckItem
    {
        public string Label { get; set; }
        public int Weight { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class InstructorApplicationStatusViewModel
    {
        [Required(ErrorMessage = "Please enter your registered email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Registered Instructor Email")]
        public string Email { get; set; }

        public bool IsSearched { get; set; }
        public bool IsFound { get; set; }
        public string SearchMessage { get; set; }
        public InstructorProfile Profile { get; set; }
    }

    public class ReviewInstructorViewModel
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string Action { get; set; } // "Approve", "RequestChanges", "Reject"

        [Display(Name = "Feedback & Actionable Instructions for Instructor")]
        public string AdminFeedback { get; set; }
    }
}
