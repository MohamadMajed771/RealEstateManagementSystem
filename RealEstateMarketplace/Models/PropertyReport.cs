
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class PropertyReport
    {
        [Key]
        public int Id { get; set; }

        // Reported property listing
        [Required]
        public int PropertyListingId { get; set; }

        public PropertyListing PropertyListing { get; set; } = null!;

        // User submitting the report
        [Required]
        public string ReportedByUserId { get; set; } = string.Empty;

        public ApplicationUser ReportedByUser { get; set; } = null!;

        // Report details
        [Required(ErrorMessage = "Report reason is required.")]
        public PropertyReportReason Reason { get; set; }

        [Required(ErrorMessage = "Please describe the issue.")]
        [StringLength(2000, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;

        // Moderation
        public PropertyReportStatus Status { get; set; } =
            PropertyReportStatus.Pending;

        // Moderator handling the report
        public string? ReviewedByUserId { get; set; }

        public ApplicationUser? ReviewedByUser { get; set; }

        [StringLength(2000)]
        public string? ModeratorNotes { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public enum PropertyReportReason
    {
        FraudOrScam = 1,
        FakePhotos = 2,
        IncorrectInformation = 3,
        DuplicateListing = 4,
        InappropriateContent = 5,
        SuspiciousSeller = 6,
        Other = 7
    }

    public enum PropertyReportStatus
    {
        Pending = 1,
        UnderReview = 2,
        Resolved = 3,
        Dismissed = 4
    }
}
