using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services.Reporting;

namespace RealEstateMarketplace.Areas.Admin.Controllers
{
    /// <summary>
    /// Admin exports: ?format=xlsx (default) or ?format=pdf.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ExportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExportController(ApplicationDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Listings(ListingStatus? status, string? format)
        {
            var query = _context.PropertyListings.AsNoTracking()
                .Include(l => l.Property).ThenInclude(p => p.Owner)
                .Where(l => !l.IsDeleted);
            if (status != null) query = query.Where(l => l.Status == status);

            var rows = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();

            var report = new TableReport
            {
                Title = "Property listings",
                Subtitle = $"{(status?.ToString() ?? "All statuses")} · generated {DateTime.Now:g}",
                Columns = new[] { "Id", "Title", "For", "Type", "City", "Area", "Price", "Currency", "Status", "Views", "Owner", "Created" }
            };
            report.Summary.Add(("Listings", rows.Count.ToString()));
            foreach (var l in rows)
                report.Rows.Add(new object?[] { l.Id, l.Title, l.ListingType.ToString(), l.Property.Type.ToString(),
                    l.Property.City, l.Property.Area, l.Price, l.Currency, l.Status.ToString(), l.ViewCount,
                    l.Property.Owner.Email, l.CreatedAt });

            return Send(report, format, "listings");
        }

        [HttpGet]
        public async Task<IActionResult> Users(string? format)
        {
            var users = await _context.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).ToListAsync();
            var adminIds = await _context.UserRoles
                .Join(_context.Roles.Where(r => r.Name == "Admin"), ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId)
                .ToListAsync();

            var report = new TableReport
            {
                Title = "Users",
                Columns = new[] { "Name", "Email", "Email confirmed", "Active", "Admin", "Joined" }
            };
            report.Summary.Add(("Users", users.Count.ToString()));
            report.Summary.Add(("Suspended", users.Count(u => !u.IsActive).ToString()));
            foreach (var u in users)
                report.Rows.Add(new object?[] { u.FirstName + " " + u.LastName, u.Email, u.EmailConfirmed, u.IsActive,
                    adminIds.Contains(u.Id), u.CreatedAt });

            return Send(report, format, "users");
        }

        [HttpGet]
        public async Task<IActionResult> Reports(string? format)
        {
            var reports = await _context.PropertyReports.AsNoTracking()
                .Include(r => r.PropertyListing)
                .Include(r => r.ReportedByUser)
                .OrderByDescending(r => r.CreatedAt).ToListAsync();

            var report = new TableReport
            {
                Title = "Property reports",
                Columns = new[] { "Id", "Listing", "Reason", "Status", "Reported by", "Description", "Decision note", "Created", "Reviewed" }
            };
            report.Summary.Add(("Reports", reports.Count.ToString()));
            foreach (var r in reports)
                report.Rows.Add(new object?[] { r.Id, r.PropertyListing.Title, r.Reason.ToString(), r.Status.ToString(),
                    r.ReportedByUser.Email, r.Description, r.ModeratorNotes, r.CreatedAt, r.ReviewedAt });

            return Send(report, format, "property-reports");
        }

        [HttpGet]
        public async Task<IActionResult> Transactions(string? format)
        {
            var transactions = await _context.Transactions.AsNoTracking()
                .Include(t => t.PropertyListing)
                .Include(t => t.Buyer)
                .Include(t => t.Seller)
                .OrderByDescending(t => t.CreatedAt).ToListAsync();

            var report = new TableReport
            {
                Title = "Transactions",
                Columns = new[] { "Id", "Type", "Listing", "Buyer / tenant", "Seller / landlord", "Agreed price", "Currency", "Status", "Created", "Completed" }
            };
            report.Summary.Add(("Transactions", transactions.Count.ToString()));
            report.Summary.Add(("Completed sales volume",
                transactions.Where(t => t.Status == TransactionStatus.Completed && t.TransactionType == ListingType.Sale)
                    .Sum(t => t.AgreedPrice).ToString("N2")));
            foreach (var t in transactions)
                report.Rows.Add(new object?[] { t.Id, t.TransactionType.ToString(), t.PropertyListing.Title, t.Buyer.Email,
                    t.Seller.Email, t.AgreedPrice, t.Currency, t.Status.ToString(), t.CreatedAt, t.CompletedAt });

            return Send(report, format, "transactions");
        }

        [HttpGet]
        public async Task<IActionResult> Payments(PaymentStatus? status, DateTime? from, DateTime? to, string? format)
        {
            var query = _context.Payments.AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.ListingPromotion).ThenInclude(lp => lp.PropertyListing)
                .Include(p => p.ListingPromotion).ThenInclude(lp => lp.PromotionPlan)
                .AsQueryable();
            if (status != null) query = query.Where(p => p.Status == status);
            if (from != null) query = query.Where(p => p.CreatedAt >= from.Value.ToUniversalTime());
            if (to != null) query = query.Where(p => p.CreatedAt < to.Value.ToUniversalTime().AddDays(1));

            var payments = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            var report = new TableReport
            {
                Title = "Promotion payments",
                Subtitle = $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd} · {status?.ToString() ?? "all statuses"} · generated {DateTime.Now:g}",
                Columns = new[] { "Id", "Date", "Owner", "Listing", "Plan", "Days", "Amount", "Currency", "Status", "Reference" }
            };
            report.Summary.Add(("Payments", payments.Count.ToString()));
            report.Summary.Add(("Revenue (succeeded)",
                payments.Where(p => p.Status == PaymentStatus.Succeeded).Sum(p => p.Amount).ToString("N2") + " USD"));
            foreach (var p in payments)
                report.Rows.Add(new object?[] { p.Id, p.CreatedAt, p.User.Email, p.ListingPromotion.PropertyListing.Title,
                    p.ListingPromotion.PromotionPlan.Name, p.ListingPromotion.DurationDays, p.Amount, p.Currency,
                    p.Status.ToString(), p.ProviderTransactionId ?? p.FailureReason });

            return Send(report, format, "promotion-payments");
        }

        private FileContentResult Send(TableReport report, string? format, string name)
        {
            var file = ReportExporter.Export(report, format, name);
            return File(file.Content, file.ContentType, file.FileName);
        }
    }
}
