
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class PromotionPlan
    {
        [Key]
        public int Id { get; set; }

        // Promotion plan name
        [Required(ErrorMessage = "Plan name is required.")]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        // Description of the plan
        [StringLength(1000)]
        public string? Description { get; set; }

        // Promotion price
        [Range(typeof(decimal), "0", "999999.99",
            ErrorMessage = "Price must be zero or greater.")]
        public decimal Price { get; set; }

        // Currency
        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        // Number of days the promotion lasts
        [Range(1, 365,
            ErrorMessage = "Duration must be between 1 and 365 days.")]
        public int DurationDays { get; set; }

        // Promotion visibility level
        [Required]
        [EnumDataType(typeof(PromotionLevel))]
        public PromotionLevel Level { get; set; }

        // Whether the plan is available for purchase
        public bool IsActive { get; set; } = true;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Related listing promotions
        public ICollection<ListingPromotion> ListingPromotions
        { get; set; } = new List<ListingPromotion>();
    }

    public enum PromotionLevel
    {
        Featured = 1,
        Premium = 2,
        Spotlight = 3
    }
}
