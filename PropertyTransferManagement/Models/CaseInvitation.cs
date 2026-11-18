
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseInvitation
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [EnumDataType(typeof(CaseParticipantRole))]
        public CaseParticipantRole IntendedRole { get; set; }

        // Store only a cryptographic hash, never the raw token
        [Required]
        [StringLength(64, MinimumLength = 64)]
        public string TokenHash { get; set; } = string.Empty;

        [Required]
        [EnumDataType(typeof(CaseInvitationStatus))]
        public CaseInvitationStatus Status { get; set; } =
            CaseInvitationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public DateTime? AcceptedAt { get; set; }

        public string? AcceptedByUserId { get; set; }

        public ApplicationUser? AcceptedByUser { get; set; }
    }

    public enum CaseInvitationStatus
    {
        Pending = 1,
        Accepted = 2,
        Expired = 3,
        Revoked = 4
    }
}
