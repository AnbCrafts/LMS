using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class User
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid Email Address format.")]
        [StringLength(120, ErrorMessage = "Email cannot exceed 120 characters.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 4, ErrorMessage = "Password must be at least 4 characters.")]
        public string Password { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; } = "Student"; // "Student", "Instructor", "Admin"

        [Display(Name = "Is Approved")]
        public bool IsApproved { get; set; } = true;

        [Display(Name = "Approval Status")]
        public string ApprovalStatus { get; set; } = "Approved"; // "Approved", "Pending", "Rejected"

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
