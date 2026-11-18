
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseAuditLog
    {
        [Key]
        public long Id { get; set; }

        // Nullable for automated system actions
        public string? UserId { get; set; }

        public ApplicationUser? User { get; set; }

        public Guid? TransferCaseId { get; set; }

        public TransferCase? TransferCase { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty;

        [StringLength(100)]
        public string? EntityId { get; set; }

        [StringLength(2000)]
        public string? Description { get; set; }

        // JSON snapshots of non-sensitive changes
        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        [StringLength(45)]
        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
