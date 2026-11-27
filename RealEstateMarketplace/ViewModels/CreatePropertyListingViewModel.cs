
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.ViewModels
{
    public partial class CreatePropertyListingViewModel
    {
        // ==========================================
        // PROPERTY INFORMATION
        // ==========================================

        [Required(ErrorMessage = "Property type is required.")]
        [EnumDataType(typeof(PropertyType))]
        [Display(Name = "Property Type")]
        public PropertyType? Type { get; set; }

        [Required(ErrorMessage = "Country is required.")]
        [StringLength(100)]
        public string Country { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required.")]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Area is required.")]
        [StringLength(150)]
        public string Area { get; set; } = string.Empty;

        [StringLength(250)]
        [Display(Name = "Street Address")]
        public string? StreetAddress { get; set; }

        [Required(ErrorMessage = "Property size is required.")]
        [Range(typeof(decimal), "0.01", "100000000")]
        [Display(Name = "Size (m²)")]
        public decimal? SizeSquareMeters { get; set; }

        [Range(0, 1000)]
        public int? Bedrooms { get; set; }

        [Range(0, 1000)]
        public int? Bathrooms { get; set; }

        // ==========================================
        // PROPERTY LOCATION (MAP)
        // ==========================================

        [Required(ErrorMessage =
            "Please select the property location on the map.")]
        [Range(typeof(decimal), "-90", "90",
            ErrorMessage = "Invalid latitude.")]
        public decimal? Latitude { get; set; }

        [Required(ErrorMessage =
            "Please select the property location on the map.")]
        [Range(typeof(decimal), "-180", "180",
            ErrorMessage = "Invalid longitude.")]
        public decimal? Longitude { get; set; }

        // ==========================================
        // LISTING INFORMATION
        // ==========================================

        [Required(ErrorMessage = "Listing title is required.")]
        [StringLength(150, MinimumLength = 5)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(5000, MinimumLength = 20)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Listing type is required.")]
        [EnumDataType(typeof(ListingType))]
        [Display(Name = "Listing Type")]
        public ListingType? ListingType { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Range(typeof(decimal), "0.01", "999999999999.99")]
        public decimal? Price { get; set; }

        [Required(ErrorMessage = "Please select a currency.")]
        [RegularExpression("^(USD|EUR|LBP)$",
            ErrorMessage = "Please select a valid currency.")]
        [Display(Name = "Currency")]
        public string Currency { get; set; } = "USD";

        // ==========================================
        // PROPERTY PHOTOS
        // ==========================================

        [Display(Name = "Property Photos")]
        public List<IFormFile> Photos { get; set; } = new();
    }
}
