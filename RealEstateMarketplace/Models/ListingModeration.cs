
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class ListingModeration
    {
        [Key]
        public int Id { get; set; }

        // Listing being reviewed
        [Required]
        public int PropertyListingId { get; set; }

        public PropertyListing PropertyListing { get; set; } = null!;

        // Moderator who made the decision
        [Required]
        public string ModeratorId { get; set; } = string.Empty;

        public ApplicationUser Moderator { get; set; } = null!;

        // Moderation decision
        [Required]
        [EnumDataType(typeof(ModerationDecision))]
        public ModerationDecision Decision { get; set; }

        // Reason for approval or rejection
        [StringLength(2000)]
        public string? Reason { get; set; }

        // Optional internal moderator notes
        [StringLength(2000)]
        public string? InternalNotes { get; set; }

        // Timestamp
        public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ModerationDecision
    {
        Approved = 1,
        Rejected = 2,
        RequestedChanges = 3
    }
}
