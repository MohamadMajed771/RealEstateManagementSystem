
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class ListingPromotion
    {
        [Key]
        public int Id { get; set; }

        // Property listing being promoted
        [Required]
        public int PropertyListingId { get; set; }

        public PropertyListing PropertyListing { get; set; } = null!;

        // Selected promotion plan
        [Required]
        public int PromotionPlanId { get; set; }

        public PromotionPlan PromotionPlan { get; set; } = null!;

        // Seller purchasing the promotion
        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        // Snapshot of the price at purchase time
        [Range(typeof(decimal), "0", "999999.99")]
        public decimal AmountPaid { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        // Snapshot of the purchased duration
        [Range(1, 365)]
        public int DurationDays { get; set; }

        // Promotion status
        [Required]
        [EnumDataType(typeof(ListingPromotionStatus))]
        public ListingPromotionStatus Status { get; set; } =
            ListingPromotionStatus.PendingPayment;

        // Promotion dates
        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }

    public enum ListingPromotionStatus
    {
        PendingPayment = 1,
        Active = 2,
        Expired = 3,
        Cancelled = 4,
        Failed = 5
    }
}
