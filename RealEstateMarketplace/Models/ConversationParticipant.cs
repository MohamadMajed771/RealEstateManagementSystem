
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class ConversationParticipant
    {
        [Key]
        public int Id { get; set; }

        // Conversation relationship
        [Required]
        public int ConversationId { get; set; }

        public Conversation Conversation { get; set; } = null!;

        // User relationship
        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        // Participant's role in the conversation
        [Required]
        public ConversationParticipantRole Role { get; set; }

        // Date the user joined
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Date the user last read the conversation
        public DateTime? LastReadAt { get; set; }

        // Participant can leave or be removed
        public bool IsActive { get; set; } = true;
    }

    public enum ConversationParticipantRole
    {
        Buyer = 1,
        Seller = 2
    }
}
