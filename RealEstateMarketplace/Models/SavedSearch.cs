
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class SavedSearch : IValidatableObject
    {
        [Key]
        public int Id { get; set; }

        // User who saved the search
        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        // Name chosen by the user
        [Required(ErrorMessage = "Search name is required.")]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        // Location filters
        [StringLength(100)]
        public string? Country { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(150)]
        public string? Area { get; set; }

        // Property filters
        public PropertyType? PropertyType { get; set; }

        public ListingType? ListingType { get; set; }

        // Price filters
        [Range(typeof(decimal), "0", "999999999999.99")]
        public decimal? MinPrice { get; set; }

        [Range(typeof(decimal), "0", "999999999999.99")]
        public decimal? MaxPrice { get; set; }

        // Size filters
        [Range(typeof(decimal), "0.01", "100000000")]
        public decimal? MinSizeSquareMeters { get; set; }

        [Range(typeof(decimal), "0.01", "100000000")]
        public decimal? MaxSizeSquareMeters { get; set; }

        // Room filters
        [Range(0, 1000)]
        public int? MinBedrooms { get; set; }

        [Range(0, 1000)]
        public int? MinBathrooms { get; set; }

        // Extra filters (spec §11: "within a selected geographic area")
        public bool? HasParking { get; set; }

        public bool? Furnished { get; set; }

        // Point chosen on the map + distance around it
        [Range(-90, 90)]
        public double? Latitude { get; set; }

        [Range(-180, 180)]
        public double? Longitude { get; set; }

        [Range(0.1, 500)]
        public double? RadiusKm { get; set; }

        // Notification preferences
        public bool EmailNotificationsEnabled { get; set; } = false;

        public bool InAppNotificationsEnabled { get; set; } = true;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Validate relationships between filter values
        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (MinPrice.HasValue && MaxPrice.HasValue &&
                MinPrice > MaxPrice)
            {
                yield return new ValidationResult(
                    "Minimum price cannot exceed maximum price.",
                    new[] { nameof(MinPrice), nameof(MaxPrice) });
            }

            if (MinSizeSquareMeters.HasValue &&
                MaxSizeSquareMeters.HasValue &&
                MinSizeSquareMeters > MaxSizeSquareMeters)
            {
                yield return new ValidationResult(
                    "Minimum size cannot exceed maximum size.",
                    new[]
                    {
                        nameof(MinSizeSquareMeters),
                        nameof(MaxSizeSquareMeters)
                    });
            }
        }
    }
}
