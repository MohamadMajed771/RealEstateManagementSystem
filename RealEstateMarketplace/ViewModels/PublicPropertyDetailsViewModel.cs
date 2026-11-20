
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.ViewModels
{
    public class PublicPropertyDetailsViewModel
    {
        public int Id { get; set; }

        // Listing information
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public ListingType ListingType { get; set; }
        public PropertyType PropertyType { get; set; }

        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";

        // Property location
        public string Country { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string? StreetAddress { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // Property specifications
        public decimal SizeSquareMeters { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }

        // Seller information
        public string SellerName { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;

        // Photos
        public List<PublicPropertyPhotoViewModel> Photos { get; set; }
            = new();

        // Publication information
        public DateTime? PublishedAt { get; set; }
    }

    public class PublicPropertyPhotoViewModel
    {
        public int Id { get; set; }

        public string ImagePath { get; set; } = string.Empty;

        public string? Caption { get; set; }

        public bool IsCoverPhoto { get; set; }

        public int DisplayOrder { get; set; }
    }
}
