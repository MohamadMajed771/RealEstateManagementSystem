
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class TransferCase
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // IDs received from the marketplace through RabbitMQ
        [Required]
        public Guid MarketplaceTransferRequestId { get; set; }

        [Required]
        public int MarketplaceTransactionId { get; set; }

        [Required]
        public int MarketplacePropertyListingId { get; set; }

        // Property information snapshot
        [Required]
        [StringLength(200)]
        public string PropertyTitle { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Country { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Area { get; set; }

        [Range(typeof(decimal), "0.01", "999999999999.99")]
        public decimal AgreedPrice { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        // Transfer processing
        [Required]
        [EnumDataType(typeof(TransferCaseStatus))]
        public TransferCaseStatus Status { get; set; } =
            TransferCaseStatus.PendingRegistration;

        [StringLength(2000)]
        public string? InternalNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        public ICollection<CaseParticipant> Participants
        { get; set; } = new List<CaseParticipant>();

        public ICollection<CaseInvitation> Invitations
        { get; set; } = new List<CaseInvitation>();

        public ICollection<CaseDocument> Documents
        { get; set; } = new List<CaseDocument>();

        public ICollection<CaseStatusHistory> StatusHistory
        { get; set; } = new List<CaseStatusHistory>();

        public ICollection<OfficerAssignment> OfficerAssignments
        { get; set; } = new List<OfficerAssignment>();
    }

    public enum TransferCaseStatus
    {
        PendingRegistration = 1,
        PendingDocuments = 2,
        UnderReview = 3,
        AdditionalDocumentsRequired = 4,
        ApprovedForOfficialProcessing = 5,
        AwaitingOfficialConfirmation = 6,
        Completed = 7,
        Rejected = 8,
        Cancelled = 9
    }
}
