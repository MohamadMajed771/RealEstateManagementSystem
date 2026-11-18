
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class LoanAssistanceRequest : IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        public string RequestedByUserId { get; set; } = string.Empty;

        public ApplicationUser RequestedByUser { get; set; } = null!;

        [Range(typeof(decimal), "0.01", "999999999999.99")]
        public decimal RequestedAmount { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        [StringLength(200)]
        public string? PreferredBank { get; set; }

        [StringLength(2000)]
        public string? AdditionalInformation { get; set; }

        [Required]
        [EnumDataType(typeof(LoanAssistanceStatus))]
        public LoanAssistanceStatus Status { get; set; } =
            LoanAssistanceStatus.Pending;

        public string? ReviewedByUserId { get; set; }

        public ApplicationUser? ReviewedByUser { get; set; }

        [StringLength(2000)]
        public string? ReviewNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (Status == LoanAssistanceStatus.Rejected &&
                string.IsNullOrWhiteSpace(ReviewNotes))
            {
                yield return new ValidationResult(
                    "A rejection reason is required.",
                    new[] { nameof(ReviewNotes) });
            }
        }
    }

    public enum LoanAssistanceStatus
    {
        Pending = 1,
        UnderReview = 2,
        InformationRequested = 3,
        ReferredToBank = 4,
        Rejected = 5,
        Cancelled = 6,
        Closed = 7
    }
}
