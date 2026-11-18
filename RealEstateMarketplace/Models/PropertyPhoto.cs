
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class PropertyPhoto
    {
        [Key]
        public int Id { get; set; }

        // Relationship with PropertyListing
        [Required]
        public int PropertyListingId { get; set; }

        public PropertyListing PropertyListing { get; set; } = null!;

        // Stored image path
        [Required(ErrorMessage = "Photo path is required.")]
        [StringLength(500)]
        public string ImagePath { get; set; } = string.Empty;

        // Optional image description
        [StringLength(200)]
        public string? Caption { get; set; }

        // Main image displayed in search results
        public bool IsCoverPhoto { get; set; } = false;

        // Display order in the photo gallery
        [Range(0, 100)]
        public int DisplayOrder { get; set; } = 0;

        // Upload timestamp
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
