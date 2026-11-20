
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class PropertyInquiry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PropertyListingId { get; set; }

        public PropertyListing PropertyListing { get; set; } = null!;

        [Required]
        public string RequesterId { get; set; } = string.Empty;

        public ApplicationUser Requester { get; set; } = null!;

        [Required]
        public string OwnerId { get; set; } = string.Empty;

        public ApplicationUser Owner { get; set; } = null!;

        [Required]
        public ListingType RequestType { get; set; }

        [Required]
        public PropertyInquiryStatus Status { get; set; }
            = PropertyInquiryStatus.Pending;

        [StringLength(2000)]
        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RespondedAt { get; set; }
    }

    public enum PropertyInquiryStatus
    {
        Pending = 1,
        Accepted = 2,
        Rejected = 3,
        Cancelled = 4
    }
}
