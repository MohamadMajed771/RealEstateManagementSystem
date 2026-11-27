
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class TransferRequest
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public int TransactionId { get; set; }

        public Transaction Transaction { get; set; } = null!;

        [Required]
        [EnumDataType(typeof(TransferRequestStatus))]
        public TransferRequestStatus Status { get; set; } =
            TransferRequestStatus.Pending;

        // Reference received from the second application
        public Guid? ExternalTransferCaseId { get; set; }

        // Case number in the Transfer Service, e.g. TR-2026-0001
        [StringLength(30)]
        public string? CaseNumber { get; set; }

        // Latest case status received through RabbitMQ
        [StringLength(100)]
        public string? ExternalStatus { get; set; }

        public DateTime? ExternalStatusUpdatedAt { get; set; }

        [StringLength(1000)]
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        public DateTime? AcknowledgedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public enum TransferRequestStatus
    {
        Pending = 1,
        Sent = 2,
        Accepted = 3,
        InProgress = 4,
        Completed = 5,
        Rejected = 6,
        Cancelled = 7,

        // The buyer or seller asked to cancel; waiting for the Transfer Service to confirm.
        CancellationRequested = 8
    }
}
