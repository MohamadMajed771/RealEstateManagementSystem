
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class DocumentType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Code { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        // Whether this document type is available for use
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<CaseDocumentRequirement> Requirements
        { get; set; } = new List<CaseDocumentRequirement>();

        public ICollection<CaseDocument> Documents
        { get; set; } = new List<CaseDocument>();
    }
}
