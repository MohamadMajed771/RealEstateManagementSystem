namespace RealEstateMarketplace.Areas.Admin.Models
{
    public class AdminUserRow
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public bool IsActive { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ListingCount { get; set; }
        public int ReportsAgainst { get; set; }
    }

    /// <summary>One point of a chart series (label + value).</summary>
    public record ChartPoint(string Label, decimal Value);

    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int TotalListings { get; set; }
        public int PendingListings { get; set; }
        public int ApprovedListings { get; set; }
        public int RejectedListings { get; set; }
        public int OpenReports { get; set; }
        public int TotalReports { get; set; }
        public int CompletedTransactions { get; set; }
        public int ActivePromotions { get; set; }
        public decimal PromotionRevenue { get; set; }
        public decimal SalesVolume { get; set; }
        public int PublishedForSale { get; set; }
        public int PublishedForRent { get; set; }

        /// <summary>Most viewed published listings (id, title, city, views).</summary>
        public List<(int Id, string Title, string City, int Views)> MostViewed { get; set; } = new();

        public List<ChartPoint> ListingsPerMonth { get; set; } = new();
        public List<ChartPoint> UsersPerMonth { get; set; } = new();
        public List<ChartPoint> ListingsByStatus { get; set; } = new();
        public List<ChartPoint> ListingsByType { get; set; } = new();
        public List<ChartPoint> ListingsByCity { get; set; } = new();
        public List<ChartPoint> ReportsByReason { get; set; } = new();
        public List<ChartPoint> RevenuePerMonth { get; set; } = new();
    }
}
