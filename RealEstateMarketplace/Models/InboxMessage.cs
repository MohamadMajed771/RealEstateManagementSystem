
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class InboxMessage
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Unique event ID from RabbitMQ message envelope
        [Required]
        public Guid MessageId { get; set; }

        [Required]
        [StringLength(200)]
        public string EventType { get; set; } = string.Empty;

        public Guid CorrelationId { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }

        [Required]
        [EnumDataType(typeof(InboxMessageStatus))]
        public InboxMessageStatus Status { get; set; } =
            InboxMessageStatus.Received;

        [Range(0, int.MaxValue)]
        public int RetryCount { get; set; } = 0;

        [StringLength(2000)]
        public string? LastError { get; set; }
    }

    public enum InboxMessageStatus
    {
        Received = 1,
        Processing = 2,
        Processed = 3,
        Failed = 4
    }
}
