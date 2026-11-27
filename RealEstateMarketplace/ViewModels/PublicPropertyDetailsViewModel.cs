
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.ViewModels
{
    public class PublicPropertyDetailsViewModel
    {
        public int Id { get; set; }

        /// <summary>Approved, or Sold / Rented (read-only page).</summary>
        public ListingStatus Status { get; set; } = ListingStatus.Approved;

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

        // Facilities and terms
        public int? ParkingSpaces { get; set; }
        public bool IsFurnished { get; set; }
        public bool HasBalcony { get; set; }
        public bool HasGarden { get; set; }
        public bool HasTerrace { get; set; }
        public string? OtherFacilities { get; set; }
        public int? RentalPeriodMonths { get; set; }
        public bool InstallmentsAccepted { get; set; }
        public int? InstallmentMonths { get; set; }
        public bool IsFeatured { get; set; }
        public int ViewCount { get; set; }

        // Owner information
        public DateTime SellerMemberSince { get; set; }
        public int SellerPublishedListings { get; set; }

        // Reviews from verified (completed) transactions
        public List<PropertyReviewItem> Reviews { get; set; } = new();
        public double? AverageRating => Reviews.Count == 0 ? null : Reviews.Average(r => r.Rating);
    }

    public class PublicPropertyPhotoViewModel
    {
        public int Id { get; set; }

        public string ImagePath { get; set; } = string.Empty;

        public string? Caption { get; set; }

        public bool IsCoverPhoto { get; set; }

        public int DisplayOrder { get; set; }
    }

    public class PropertyReviewItem
    {
        public string ReviewerName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public int? AccuracyRating { get; set; }
        public int? ConditionRating { get; set; }
        public int? CommunicationRating { get; set; }
        public bool IsRental { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
