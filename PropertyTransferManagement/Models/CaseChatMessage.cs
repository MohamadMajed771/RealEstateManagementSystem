
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseChatMessage
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public int CaseConversationId { get; set; }

        public CaseConversation CaseConversation { get; set; } = null!;

        [Required]
        public string SenderId { get; set; } = string.Empty;

        public ApplicationUser Sender { get; set; } = null!;

        [Required]
        [StringLength(
            3000,
            MinimumLength = 1,
            ErrorMessage = "Message must contain between 1 and 3000 characters.")]
        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DateTime? EditedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }
    }
}
