
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class CaseDocument
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        public int DocumentTypeId { get; set; }

        public DocumentType DocumentType { get; set; } = null!;

        // Optional requirement fulfilled by this document
        public int? CaseDocumentRequirementId { get; set; }

        public CaseDocumentRequirement? CaseDocumentRequirement
        { get; set; }

        [Required]
        public string UploadedByUserId { get; set; } = string.Empty;

        public ApplicationUser UploadedByUser { get; set; } = null!;

        // Secure server-generated file reference
        [Required]
        [StringLength(500)]
        public string StorageKey { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Range(1, 20971520)]
        public long FileSizeBytes { get; set; }

        [StringLength(64, MinimumLength = 64)]
        public string? Sha256Hash { get; set; }

        [Required]
        [EnumDataType(typeof(CaseDocumentStatus))]
        public CaseDocumentStatus Status { get; set; } =
            CaseDocumentStatus.PendingReview;

        public int Version { get; set; } = 1;

        // Details read from an image with OCR (in the uploader's browser) and
        // checked/corrected by the uploader. Not legally verified (spec §24):
        // the officer still compares them with the document itself.
        [StringLength(2000)]
        public string? UploaderConfirmedDetails { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        public ICollection<DocumentReview> Reviews
        { get; set; } = new List<DocumentReview>();
    }

    public enum CaseDocumentStatus
    {
        PendingReview = 1,
        Approved = 2,
        Rejected = 3,
        Superseded = 4
    }
}
