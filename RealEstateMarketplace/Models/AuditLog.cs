
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class AuditLog
    {
        [Key]
        public long Id { get; set; }

        // User who performed the action
        // Null for automated system actions
        public string? UserId { get; set; }

        public ApplicationUser? User { get; set; }

        // Action details
        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty;

        [StringLength(100)]
        public string? EntityId { get; set; }

        // Optional description
        [StringLength(2000)]
        public string? Description { get; set; }

        // Changes recorded as JSON
        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        // Request information
        [StringLength(45)]
        public string? IpAddress { get; set; }

        // Timestamp
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
