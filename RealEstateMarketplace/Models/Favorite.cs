
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class Favorite
    {
        [Key]
        public int Id { get; set; }

        // User who saved the listing
        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        // Saved property listing
        [Required]
        public int PropertyListingId { get; set; }

        public PropertyListing PropertyListing { get; set; } = null!;

        // Date added to favorites
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
