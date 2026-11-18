
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, MinimumLength = 2)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50, MinimumLength = 2)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? JobTitle { get; set; }

        // Officer-specific information
        [StringLength(50)]
        public string? EmployeeNumber { get; set; }

        // Account status
        public bool IsActive { get; set; } = true;

        // Account creation date
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }
    }
}
