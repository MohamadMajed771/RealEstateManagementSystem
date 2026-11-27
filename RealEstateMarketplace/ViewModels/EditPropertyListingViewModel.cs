
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.ViewModels
{
    public partial class EditPropertyListingViewModel
    {
        // ==========================================
        // LISTING IDENTIFIER
        // ==========================================

        public int Id { get; set; }

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
        // PROPERTY LOCATION
        // ==========================================

        [Required(ErrorMessage = "Select the location on the map.")]
        [Range(typeof(decimal), "-90", "90")]
        public decimal? Latitude { get; set; }

        [Required(ErrorMessage = "Select the location on the map.")]
        [Range(typeof(decimal), "-180", "180")]
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

        [Required(ErrorMessage = "Currency is required.")]
        [RegularExpression("^(USD|EUR|LBP)$",
            ErrorMessage = "Please select a valid currency.")]
        public string Currency { get; set; } = "USD";

        // ==========================================
        // EXISTING PROPERTY PHOTOS
        // ==========================================

        // Photos already saved in SQL Server.
        public List<PropertyPhoto> ExistingPhotos { get; set; } = new();

        // ==========================================
        // ADD NEW PHOTOS
        // ==========================================

        [Display(Name = "Add New Photos")]
        public List<IFormFile> NewPhotos { get; set; } = new();

        // ==========================================
        // DELETE EXISTING PHOTOS
        // ==========================================

        // IDs of photos selected for deletion.
        public List<int> PhotoIdsToDelete { get; set; } = new();

        // ==========================================
        // CHANGE COVER PHOTO
        // ==========================================

        // ID of the selected cover photo.
        public int? CoverPhotoId { get; set; }
    }
}
