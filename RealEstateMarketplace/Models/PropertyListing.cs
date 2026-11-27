
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class PropertyListing
    {
        [Key]
        public int Id { get; set; }

        // Relationship with Property
        [Required]
        public int PropertyId { get; set; }

        public Property Property { get; set; } = null!;

        // Advertisement details
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(150, MinimumLength = 5)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(5000, MinimumLength = 20)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public ListingType ListingType { get; set; }

        [Range(typeof(decimal), "0.01", "999999999999.99",
            ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        // Rent: proposed rental period, e.g. 3, 6 or 12 months.
        [Range(1, 120)]
        public int? RentalPeriodMonths { get; set; }

        // Sale: installments are only a listing detail (no financing).
        public bool InstallmentsAccepted { get; set; }

        [Range(1, 360)]
        public int? InstallmentMonths { get; set; }

        // Number of times the public details page was opened.
        public int ViewCount { get; set; }

        // Set while a paid promotion is active.
        public DateTime? FeaturedUntil { get; set; }

        // Moderation status
        public ListingStatus Status { get; set; } =
            ListingStatus.PendingApproval;

        // Publication dates
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        // Soft deletion
        public bool IsDeleted { get; set; } = false;
    }

    public enum ListingType
    {
        Sale = 1,
        Rent = 2
    }

    public enum ListingStatus
    {
        PendingApproval = 1,
        Approved = 2,
        Rejected = 3,
        Sold = 4,
        Rented = 5,
        Archived = 6
    }
}
