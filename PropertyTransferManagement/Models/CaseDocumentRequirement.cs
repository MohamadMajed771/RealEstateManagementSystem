
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseDocumentRequirement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        public int DocumentTypeId { get; set; }

        public DocumentType DocumentType { get; set; } = null!;

        [Required]
        [EnumDataType(typeof(CaseParticipantRole))]
        public CaseParticipantRole RequiredFromRole { get; set; }

        public bool IsMandatory { get; set; } = true;

        [Required]
        [EnumDataType(typeof(DocumentRequirementStatus))]
        public DocumentRequirementStatus Status { get; set; } =
            DocumentRequirementStatus.Pending;

        [StringLength(1000)]
        public string? Instructions { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SatisfiedAt { get; set; }
    }

    public enum DocumentRequirementStatus
    {
        Pending = 1,
        Submitted = 2,
        Approved = 3,
        Rejected = 4,
        Waived = 5
    }
}
