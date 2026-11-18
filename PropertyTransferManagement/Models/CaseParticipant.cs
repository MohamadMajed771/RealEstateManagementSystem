
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseParticipant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        [EnumDataType(typeof(CaseParticipantRole))]
        public CaseParticipantRole Role { get; set; }

        public bool HasConfirmedDetails { get; set; } = false;

        public DateTime? ConfirmedAt { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
    }

    public enum CaseParticipantRole
    {
        Buyer = 1,
        Seller = 2,
        AuthorizedRepresentative = 3
    }
}
