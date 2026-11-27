
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class Property
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string OwnerId { get; set; } = string.Empty;

        public ApplicationUser Owner { get; set; } = null!;

        [Required(ErrorMessage = "Property type is required.")]
        public PropertyType Type { get; set; }

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
        public string? StreetAddress { get; set; }

        [Range(0.01, 100000000)]
        public decimal SizeSquareMeters { get; set; }

        [Range(0, 1000)]
        public int? Bedrooms { get; set; }

        [Range(0, 1000)]
        public int? Bathrooms { get; set; }

        [Range(-90, 90)]
        public decimal? Latitude { get; set; }

        [Range(-180, 180)]
        public decimal? Longitude { get; set; }

        // Facilities (spec section 5, step 4)
        [Range(0, 100)]
        public int? ParkingSpaces { get; set; }

        public bool IsFurnished { get; set; }

        public bool HasBalcony { get; set; }

        public bool HasGarden { get; set; }

        public bool HasTerrace { get; set; }

        [StringLength(500)]
        public string? OtherFacilities { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }

    public enum PropertyType
    {
        Apartment = 1,
        House = 2,
        Office = 3,
        Garage = 4,
        Land = 5
    }
}
