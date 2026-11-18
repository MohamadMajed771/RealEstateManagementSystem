
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        // Property listing involved in the transaction
        [Required]
        public int PropertyListingId { get; set; }

        public PropertyListing PropertyListing { get; set; } = null!;

        // Buyer
        [Required]
        public string BuyerId { get; set; } = string.Empty;

        public ApplicationUser Buyer { get; set; } = null!;

        // Seller
        [Required]
        public string SellerId { get; set; } = string.Empty;

        public ApplicationUser Seller { get; set; } = null!;

        // Agreed transaction details
        [Range(typeof(decimal), "0.01", "999999999999.99",
            ErrorMessage = "Agreed price must be greater than zero.")]
        public decimal AgreedPrice { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        [Required]
        public TransactionStatus Status { get; set; } =
            TransactionStatus.PendingConfirmation;

        // Consent from both parties
        public bool BuyerConfirmed { get; set; } = false;

        public bool SellerConfirmed { get; set; } = false;

        public DateTime? BuyerConfirmedAt { get; set; }

        public DateTime? SellerConfirmedAt { get; set; }

        // Ownership transfer tracking
        public Guid? TransferRequestId { get; set; }

        public DateTime? TransferRequestedAt { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }

    public enum TransactionStatus
    {
        PendingConfirmation = 1,
        Confirmed = 2,
        TransferRequested = 3,
        TransferInProgress = 4,
        Completed = 5,
        Cancelled = 6,
        Rejected = 7
    }
}
