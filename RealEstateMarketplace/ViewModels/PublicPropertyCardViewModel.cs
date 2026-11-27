
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.ViewModels
{
    public class PublicPropertyCardViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public PropertyType PropertyType { get; set; }

        public ListingType ListingType { get; set; }

        public decimal Price { get; set; }

        public string Currency { get; set; } = "USD";

        public string City { get; set; } = string.Empty;

        public string Area { get; set; } = string.Empty;

        public decimal SizeSquareMeters { get; set; }

        public int? Bedrooms { get; set; }

        public int? Bathrooms { get; set; }

        public string? CoverPhotoPath { get; set; }

        public int? ParkingSpaces { get; set; }

        public bool IsFurnished { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public int? RentalPeriodMonths { get; set; }

        // Paid promotion is active
        public bool IsFeatured { get; set; }

        // Set when searching around a point on the map
        public double? DistanceKm { get; set; }

        // AI search: soft preferences found in the listing text
        public List<string> MatchedKeywords { get; set; } = new();

        // Lower-case text used for keyword matching (not displayed)
        public string SearchText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
