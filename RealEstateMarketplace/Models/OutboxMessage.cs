
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class OutboxMessage
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(200)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string RoutingKey { get; set; } = string.Empty;

        [Required]
        public string Payload { get; set; } = string.Empty;

        public Guid CorrelationId { get; set; }

        public int SchemaVersion { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }

        public DateTime? NextAttemptAt { get; set; }

        [Range(0, int.MaxValue)]
        public int RetryCount { get; set; } = 0;

        [StringLength(2000)]
        public string? LastError { get; set; }

        [Required]
        [EnumDataType(typeof(OutboxMessageStatus))]
        public OutboxMessageStatus Status { get; set; } =
            OutboxMessageStatus.Pending;
    }

    public enum OutboxMessageStatus
    {
        Pending = 1,
        Processing = 2,
        Published = 3,
        Failed = 4
    }
}
