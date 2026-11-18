
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class UserVerification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        [EnumDataType(typeof(VerificationType))]
        public VerificationType Type { get; set; }

        [Required]
        [EnumDataType(typeof(VerificationStatus))]
        public VerificationStatus Status { get; set; } =
            VerificationStatus.Pending;

        // Optional related uploaded document
        public int? UserDocumentId { get; set; }

        public UserDocument? UserDocument { get; set; }

        // OCR-assisted extraction
        [StringLength(100)]
        public string? ExtractedFirstName { get; set; }

        [StringLength(100)]
        public string? ExtractedLastName { get; set; }

        [StringLength(100)]
        public string? ExtractedDocumentNumber { get; set; }

        // Administrator reviewing verification
        public string? ReviewedByUserId { get; set; }

        public ApplicationUser? ReviewedByUser { get; set; }

        [StringLength(1000)]
        public string? ReviewNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }

    public enum VerificationType
    {
        GovernmentId = 1
    }

    public enum VerificationStatus
    {
        Pending = 1,
        UnderReview = 2,
        Approved = 3,
        Rejected = 4,
        Expired = 5
    }
}
