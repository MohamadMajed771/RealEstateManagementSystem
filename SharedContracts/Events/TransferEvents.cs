namespace SharedContracts.Events
{
    // ==========================================
    // RabbitMQ message contracts exchanged between
    // System 1 (Marketplace) and System 2 (Transfer Service).
    //
    // Rule: only the minimum information the receiving
    // system needs. No ID images, legal documents, bank
    // statements or chat messages ever travel in an event.
    // ==========================================

    /// <summary>
    /// Common fields of every transfer-related event.
    /// TransferRequestId is created by the Marketplace and
    /// correlates every message of the same transfer.
    /// </summary>
    public abstract record TransferEvent
    {
        public Guid MessageId { get; init; } = Guid.NewGuid();

        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

        public Guid TransferRequestId { get; init; }
    }

    public record TransferParty
    {
        public string MarketplaceUserId { get; init; } = string.Empty;

        public string FullName { get; init; } = string.Empty;

        // Verified email in the Marketplace. The Transfer Service
        // uses it to link the person's own (separate) account.
        public string Email { get; init; } = string.Empty;

        public string? PhoneNumber { get; init; }
    }

    // ==========================================
    // SYSTEM 1 -> SYSTEM 2
    // ==========================================

    /// <summary>
    /// Buyer and seller agreed on a sale and the buyer
    /// requested ownership-transfer assistance.
    /// </summary>
    public record TransferRequested : TransferEvent
    {
        public int MarketplaceTransactionId { get; init; }

        public int MarketplacePropertyListingId { get; init; }

        public string PropertyTitle { get; init; } = string.Empty;

        public string PropertyType { get; init; } = string.Empty;

        public string Country { get; init; } = string.Empty;

        public string City { get; init; } = string.Empty;

        public string? Area { get; init; }

        public string? StreetAddress { get; init; }

        public decimal SizeSquareMeters { get; init; }

        public decimal AgreedPrice { get; init; }

        public string Currency { get; init; } = "USD";

        public TransferParty Buyer { get; init; } = new();

        public TransferParty Seller { get; init; } = new();
    }

    /// <summary>
    /// A participant cancelled the transfer request in the Marketplace.
    /// </summary>
    public record TransferCancelled : TransferEvent
    {
        public string Reason { get; init; } = string.Empty;
    }

    // ==========================================
    // SYSTEM 2 -> SYSTEM 1
    // ==========================================

    /// <summary>
    /// The Transfer Service created a case for the request.
    /// </summary>
    public record TransferCaseCreated : TransferEvent
    {
        public Guid CaseId { get; init; }

        public string CaseNumber { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;
    }

    /// <summary>
    /// The case moved to a new status.
    /// </summary>
    public record TransferStatusChanged : TransferEvent
    {
        public string CaseNumber { get; init; } = string.Empty;

        public string PreviousStatus { get; init; } = string.Empty;

        public string NewStatus { get; init; } = string.Empty;

        public string? Note { get; init; }
    }

    /// <summary>
    /// The officer needs documents from the buyer and/or seller.
    /// Only document type names are sent, never document contents.
    /// </summary>
    public record AdditionalDocumentsRequired : TransferEvent
    {
        public string CaseNumber { get; init; } = string.Empty;

        public bool RequiredFromBuyer { get; init; }

        public bool RequiredFromSeller { get; init; }

        public List<string> DocumentNames { get; init; } = new();

        public string? Note { get; init; }
    }

    /// <summary>
    /// An officer recorded evidence that the competent authority
    /// completed the ownership transfer.
    /// </summary>
    public record TransferCompleted : TransferEvent
    {
        public string CaseNumber { get; init; } = string.Empty;

        public string AuthorityName { get; init; } = string.Empty;

        public string OfficialReferenceNumber { get; init; } = string.Empty;

        public DateTime CompletedAt { get; init; }
    }

    /// <summary>
    /// The case was rejected or cannot proceed.
    /// </summary>
    public record TransferRejected : TransferEvent
    {
        public string CaseNumber { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;
    }

    public static class TransferStatusText
    {
        /// <summary>
        /// "DocumentsUnderReview" -> "Documents Under Review"
        /// </summary>
        public static string Humanize(string? status) =>
            string.IsNullOrEmpty(status)
                ? string.Empty
                : System.Text.RegularExpressions.Regex.Replace(status, "(?<=[a-z])(?=[A-Z])", " ");
    }
}
