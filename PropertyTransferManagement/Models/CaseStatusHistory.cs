
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseStatusHistory
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        [EnumDataType(typeof(TransferCaseStatus))]
        public TransferCaseStatus PreviousStatus { get; set; }

        [Required]
        [EnumDataType(typeof(TransferCaseStatus))]
        public TransferCaseStatus NewStatus { get; set; }

        // Nullable for system-generated status changes
        public string? ChangedByUserId { get; set; }

        public ApplicationUser? ChangedByUser { get; set; }

        [StringLength(2000)]
        public string? Reason { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}
