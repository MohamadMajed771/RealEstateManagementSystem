
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

        public DateTime CreatedAt { get; set; }
    }
}
