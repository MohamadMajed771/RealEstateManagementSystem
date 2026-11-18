
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class OfficialConfirmation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        // External authority issuing the confirmation
        [Required]
        [StringLength(200)]
        public string AuthorityName { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string OfficialReferenceNumber { get; set; } =
            string.Empty;

        [Required]
        [EnumDataType(typeof(OfficialConfirmationStatus))]
        public OfficialConfirmationStatus Status { get; set; } =
            OfficialConfirmationStatus.PendingVerification;

        // Date on the official confirmation
        public DateTime? OfficialConfirmationDate { get; set; }

        // Secure reference to supporting document
        [StringLength(500)]
        public string? ConfirmationDocumentStorageKey { get; set; }

        // Officer recording the external confirmation
        [Required]
        public string RecordedByUserId { get; set; } = string.Empty;

        public ApplicationUser RecordedByUser { get; set; } = null!;

        // Officer who verified the confirmation
        public string? VerifiedByUserId { get; set; }

        public ApplicationUser? VerifiedByUser { get; set; }

        [StringLength(2000)]
        public string? VerificationNotes { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        public DateTime? VerifiedAt { get; set; }
    }

    public enum OfficialConfirmationStatus
    {
        PendingVerification = 1,
        Verified = 2,
        Rejected = 3
    }
}
