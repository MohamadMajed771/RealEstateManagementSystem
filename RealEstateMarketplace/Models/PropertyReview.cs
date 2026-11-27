
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class PropertyReview
    {
        [Key]
        public int Id { get; set; }

        // Completed transaction being reviewed
        [Required]
        public int TransactionId { get; set; }

        public Transaction Transaction { get; set; } = null!;

        // Buyer writing the review
        [Required]
        public string ReviewerId { get; set; } = string.Empty;

        public ApplicationUser Reviewer { get; set; } = null!;

        // Seller receiving the review
        [Required]
        public string ReviewedUserId { get; set; } = string.Empty;

        public ApplicationUser ReviewedUser { get; set; } = null!;

        // Rating from 1 to 5 stars
        [Range(1, 5,
            ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        // Optional detail ratings (spec §15: accuracy, condition, communication).
        [Range(1, 5)]
        public int? AccuracyRating { get; set; }

        [Range(1, 5)]
        public int? ConditionRating { get; set; }

        [Range(1, 5)]
        public int? CommunicationRating { get; set; }

        // Optional written review
        [StringLength(2000,
            ErrorMessage = "Review cannot exceed 2000 characters.")]
        public string? Comment { get; set; }

        // Moderation
        public ReviewStatus Status { get; set; } =
            ReviewStatus.PendingApproval;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Soft deletion
        public bool IsDeleted { get; set; } = false;
    }

    public enum ReviewStatus
    {
        PendingApproval = 1,
        Approved = 2,
        Rejected = 3
    }
}
