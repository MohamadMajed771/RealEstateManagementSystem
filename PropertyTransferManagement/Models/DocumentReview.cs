
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class DocumentReview
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CaseDocumentId { get; set; }

        public CaseDocument CaseDocument { get; set; } = null!;

        [Required]
        public string ReviewedByUserId { get; set; } = string.Empty;

        public ApplicationUser ReviewedByUser { get; set; } = null!;

        [Required]
        [EnumDataType(typeof(DocumentReviewDecision))]
        public DocumentReviewDecision Decision { get; set; }

        [StringLength(2000)]
        public string? Comments { get; set; }

        public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
    }

    public enum DocumentReviewDecision
    {
        Approved = 1,
        Rejected = 2,
        RequestReplacement = 3
    }
}
