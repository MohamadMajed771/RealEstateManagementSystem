
using System;
using System.ComponentModel.DataAnnotations;

namespace RealEstateMarketplace.Models
{
    public class RentalAgreement
    {
        [Key]
        public int Id { get; set; }

        // ==========================================
        // TRANSACTION
        // ==========================================

        [Required]
        public int TransactionId { get; set; }

        public Transaction Transaction { get; set; } = null!;

        // ==========================================
        // RENTAL TERMS
        // ==========================================

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(typeof(decimal), "0.01", "999999999999.99")]
        public decimal MonthlyRent { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "USD";

        // ==========================================
        // APPROVALS
        // ==========================================

        public bool TenantApproved { get; set; } = false;

        public bool LandlordApproved { get; set; } = false;

        public DateTime? TenantApprovedAt { get; set; }

        public DateTime? LandlordApprovedAt { get; set; }

        // ==========================================
        // AGREEMENT STATUS
        // ==========================================

        public RentalAgreementStatus Status { get; set; }
            = RentalAgreementStatus.Draft;

        // ==========================================
        // REJECTION
        // ==========================================

        [StringLength(1000)]
        public string? RejectionReason { get; set; }

        public string? RejectedByUserId { get; set; }

        public DateTime? RejectedAt { get; set; }

        // ==========================================
        // TIMESTAMPS
        // ==========================================

        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;

        public DateTime? FinalizedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }

    public enum RentalAgreementStatus
    {
        Draft = 1,
        PendingApproval = 2,
        Scheduled = 3,
        Active = 4,
        Expired = 5,
        Completed = 6,
        Cancelled = 7,

        // New status for declined proposals.
        Rejected = 8
    }
}
