
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseConversationParticipant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CaseConversationId { get; set; }

        public CaseConversation CaseConversation { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        [EnumDataType(typeof(CaseConversationRole))]
        public CaseConversationRole Role { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastReadAt { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public enum CaseConversationRole
    {
        Buyer = 1,
        Seller = 2,
        Officer = 3,
        Administrator = 4,
        AuthorizedRepresentative = 5
    }
}
