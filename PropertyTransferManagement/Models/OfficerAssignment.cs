
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class OfficerAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        public string OfficerId { get; set; } = string.Empty;

        public ApplicationUser Officer { get; set; } = null!;

        // Administrator assigning the officer
        public string? AssignedByUserId { get; set; }

        public ApplicationUser? AssignedByUser { get; set; }

        [Required]
        [EnumDataType(typeof(OfficerAssignmentStatus))]
        public OfficerAssignmentStatus Status { get; set; } =
            OfficerAssignmentStatus.Active;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UnassignedAt { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public enum OfficerAssignmentStatus
    {
        Active = 1,
        Completed = 2,
        Reassigned = 3,
        Cancelled = 4
    }
}
