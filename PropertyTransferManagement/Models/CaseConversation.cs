
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseConversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public CaseConversationKind Kind { get; set; } = CaseConversationKind.Shared;

        public bool IsClosed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastMessageAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        public ICollection<CaseConversationParticipant> Participants
        { get; set; } = new List<CaseConversationParticipant>();

        public ICollection<CaseChatMessage> Messages
        { get; set; } = new List<CaseChatMessage>();
    }

    /// <summary>Private buyer/officer and seller/officer threads plus one shared thread.</summary>
    public enum CaseConversationKind
    {
        BuyerOfficer = 1,
        SellerOfficer = 2,
        Shared = 3
    }
}
