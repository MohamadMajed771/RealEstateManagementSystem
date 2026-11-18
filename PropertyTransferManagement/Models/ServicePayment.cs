
using System.ComponentModel.DataAnnotations;

namespace PropertyTransferManagement.Models
{
    public class ServicePayment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid TransferCaseId { get; set; }

        public TransferCase TransferCase { get; set; } = null!;

        [Required]
        public string PaidByUserId { get; set; } = string.Empty;

        public ApplicationUser PaidByUser { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string ServiceName { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "999999999.99")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        [Required]
        [EnumDataType(typeof(ServicePaymentStatus))]
        public ServicePaymentStatus Status { get; set; } =
            ServicePaymentStatus.Pending;

        [Required]
        [EnumDataType(typeof(ServicePaymentMethod))]
        public ServicePaymentMethod Method { get; set; } =
            ServicePaymentMethod.TestPayment;

        [StringLength(100)]
        public string? ProviderTransactionId { get; set; }

        [StringLength(100)]
        public string? IdempotencyKey { get; set; }

        [StringLength(1000)]
        public string? FailureReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PaidAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public enum ServicePaymentStatus
    {
        Pending = 1,
        Processing = 2,
        Succeeded = 3,
        Failed = 4,
        Cancelled = 5,
        Refunded = 6
    }

    public enum ServicePaymentMethod
    {
        TestPayment = 1,
        Stripe = 2
    }
}
