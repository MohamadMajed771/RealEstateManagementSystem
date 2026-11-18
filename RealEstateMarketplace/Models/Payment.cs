
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        public int ListingPromotionId { get; set; }

        public ListingPromotion ListingPromotion { get; set; } = null!;

        [Range(typeof(decimal), "0", "999999999.99")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        [Required]
        [EnumDataType(typeof(PaymentStatus))]
        public PaymentStatus Status { get; set; } =
            PaymentStatus.Pending;

        [Required]
        [EnumDataType(typeof(PaymentMethod))]
        public PaymentMethod Method { get; set; } =
            PaymentMethod.TestPayment;

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

    public enum PaymentStatus
    {
        Pending = 1,
        Processing = 2,
        Succeeded = 3,
        Failed = 4,
        Cancelled = 5,
        Refunded = 6
    }

    public enum PaymentMethod
    {
        TestPayment = 1,
        Stripe = 2
    }
}
