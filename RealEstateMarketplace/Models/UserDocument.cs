
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class UserDocument
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        [EnumDataType(typeof(UserDocumentType))]
        public UserDocumentType DocumentType { get; set; }

        // Server-generated storage reference
        [Required]
        [StringLength(500)]
        public string StorageKey { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Range(1, 10485760)]
        public long FileSizeBytes { get; set; }

        // SHA-256 checksum for integrity checks
        [StringLength(64, MinimumLength = 64)]
        public string? Sha256Hash { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public DateTime? DeletedAt { get; set; }

        public bool IsDeleted { get; set; } = false;
    }

    public enum UserDocumentType
    {
        NationalId = 1,
        Passport = 2,
        OtherIdentityDocument = 3
    }
}
