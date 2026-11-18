
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        // Conversation relationship
        [Required]
        public int ConversationId { get; set; }

        public Conversation Conversation { get; set; } = null!;

        // Sender relationship
        [Required]
        public string SenderId { get; set; } = string.Empty;

        public ApplicationUser Sender { get; set; } = null!;

        // Message content
        [Required(ErrorMessage = "Message cannot be empty.")]
        [StringLength(2000, MinimumLength = 1,
            ErrorMessage = "Message must contain between 1 and 2000 characters.")]
        public string Content { get; set; } = string.Empty;

        // Message timestamps
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DateTime? EditedAt { get; set; }

        // Soft deletion
        public bool IsDeleted { get; set; } = false;
    }
}
