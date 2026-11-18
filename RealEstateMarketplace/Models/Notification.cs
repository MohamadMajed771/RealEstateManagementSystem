
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        // User receiving the notification
        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        // Notification details
        [Required(ErrorMessage = "Notification title is required.")]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Notification message is required.")]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public NotificationType Type { get; set; }

        // Optional related record
        public int? RelatedEntityId { get; set; }

        [StringLength(100)]
        public string? RelatedEntityType { get; set; }

        // Optional internal application link
        [StringLength(500)]
        public string? ActionUrl { get; set; }

        // Read status
        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft deletion
        public bool IsDeleted { get; set; } = false;
    }

    public enum NotificationType
    {
        ListingApproved = 1,
        ListingRejected = 2,
        NewMessage = 3,
        SavedSearchMatch = 4,
        TransactionConfirmation = 5,
        TransactionStatusChanged = 6,
        TransferStatusChanged = 7,
        PaymentStatusChanged = 8,
        SystemAnnouncement = 9
    }
}
