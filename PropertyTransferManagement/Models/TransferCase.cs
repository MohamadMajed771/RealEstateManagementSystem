
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

        // Human-readable reference, e.g. TR-2026-0001
        [Required]
        [StringLength(30)]
        public string CaseNumber { get; set; } = string.Empty;

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

        [StringLength(250)]
        public string? StreetAddress { get; set; }

        [StringLength(50)]
        public string? PropertyType { get; set; }

        public decimal? SizeSquareMeters { get; set; }

        [Range(typeof(decimal), "0.01", "999999999999.99")]
        public decimal AgreedPrice { get; set; }

        // Parties as declared by the marketplace. Their accounts in
        // this system are linked when they sign in with the same
        // (confirmed) email address. See CaseInvitation.
        [Required]
        [StringLength(150)]
        public string BuyerFullName { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string BuyerEmail { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string SellerFullName { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string SellerEmail { get; set; } = string.Empty;

        // Current officer (history is kept in OfficerAssignments)
        public string? AssignedOfficerId { get; set; }

        public ApplicationUser? AssignedOfficer { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        // Transfer processing
        [Required]
        [EnumDataType(typeof(TransferCaseStatus))]
        public TransferCaseStatus Status { get; set; } =
            TransferCaseStatus.RequestReceived;

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

    // Statuses from the project specification (section 27).
    // Only an officer recording official evidence can reach
    // OfficialCompletionConfirmed (see OfficialConfirmation).
    public enum TransferCaseStatus
    {
        RequestReceived = 1,
        AwaitingDocuments = 2,
        DocumentsUnderReview = 3,
        AdditionalInformationRequired = 4,
        ReadyForProcessing = 5,
        ExternalProcessingInProgress = 6,
        OfficialCompletionConfirmed = 7,
        Rejected = 8,
        Cancelled = 9,
        CaseClosed = 10,
        OnHold = 11
    }
}
