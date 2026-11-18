
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class Conversation
    {
        [Key]
        public int Id { get; set; }

        // Property listing being discussed
        [Required]
        public int PropertyListingId { get; set; }

        public PropertyListing PropertyListing { get; set; } = null!;

        // User who started the conversation
        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        public ApplicationUser CreatedByUser { get; set; } = null!;

        // Conversation status
        public bool IsClosed { get; set; } = false;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastMessageAt { get; set; }

        // Related participants and messages
        public ICollection<ConversationParticipant> Participants
        { get; set; } = new List<ConversationParticipant>();

        public ICollection<ChatMessage> Messages
        { get; set; } = new List<ChatMessage>();
    }
}
