using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Areas.Admin.Models;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var since = monthStart.AddMonths(-11);
            var listings = _context.PropertyListings.Where(l => !l.IsDeleted);

            var vm = new AdminDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                NewUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= monthStart),
                TotalListings = await listings.CountAsync(),
                PendingListings = await listings.CountAsync(l => l.Status == ListingStatus.PendingApproval),
                ApprovedListings = await listings.CountAsync(l => l.Status == ListingStatus.Approved),
                RejectedListings = await listings.CountAsync(l => l.Status == ListingStatus.Rejected),
                TotalReports = await _context.PropertyReports.CountAsync(),
                OpenReports = await _context.PropertyReports.CountAsync(r =>
                    r.Status == PropertyReportStatus.Pending || r.Status == PropertyReportStatus.UnderReview),
                CompletedTransactions = await _context.Transactions.CountAsync(t => t.Status == TransactionStatus.Completed),
                SalesVolume = await _context.Transactions
                    .Where(t => t.Status == TransactionStatus.Completed && t.TransactionType == ListingType.Sale)
                    .SumAsync(t => (decimal?)t.AgreedPrice) ?? 0,
                ActivePromotions = await _context.ListingPromotions
                    .CountAsync(p => p.Status == ListingPromotionStatus.Active && p.EndDate > now),
                PromotionRevenue = await _context.Payments
                    .Where(p => p.Status == PaymentStatus.Succeeded)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0
            };

            vm.PublishedForSale = await listings.CountAsync(l => l.Status == ListingStatus.Approved && l.ListingType == ListingType.Sale);
            vm.PublishedForRent = await listings.CountAsync(l => l.Status == ListingStatus.Approved && l.ListingType == ListingType.Rent);

            vm.MostViewed = (await listings
                    .Where(l => l.Status == ListingStatus.Approved || l.Status == ListingStatus.Sold || l.Status == ListingStatus.Rented)
                    .OrderByDescending(l => l.ViewCount).ThenBy(l => l.Id)
                    .Take(5)
                    .Select(l => new { l.Id, l.Title, l.Property.City, l.ViewCount })
                    .ToListAsync())
                .Select(x => (x.Id, x.Title, x.City, x.ViewCount)).ToList();

            var listingMonths = await listings.Where(l => l.CreatedAt >= since)
                .GroupBy(l => new { l.CreatedAt.Year, l.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();
            vm.ListingsPerMonth = LastTwelveMonths(since, (y, m) =>
                listingMonths.FirstOrDefault(x => x.Year == y && x.Month == m)?.Count ?? 0);

            var userMonths = await _context.Users.Where(u => u.CreatedAt >= since)
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();
            vm.UsersPerMonth = LastTwelveMonths(since, (y, m) =>
                userMonths.FirstOrDefault(x => x.Year == y && x.Month == m)?.Count ?? 0);

            var revenueMonths = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Succeeded && p.PaidAt >= since)
                .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt!.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Sum = g.Sum(p => p.Amount) })
                .ToListAsync();
            vm.RevenuePerMonth = LastTwelveMonths(since, (y, m) =>
                revenueMonths.FirstOrDefault(x => x.Year == y && x.Month == m)?.Sum ?? 0);

            vm.ListingsByStatus = (await listings.GroupBy(l => l.Status)
                    .Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .OrderBy(x => x.Key).Select(x => new ChartPoint(x.Key.ToString(), x.Count)).ToList();

            vm.ListingsByType = (await listings.GroupBy(l => l.Property.Type)
                    .Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .OrderByDescending(x => x.Count).Select(x => new ChartPoint(x.Key.ToString(), x.Count)).ToList();

            vm.ListingsByCity = (await listings.Where(l => l.Status == ListingStatus.Approved)
                    .GroupBy(l => l.Property.City)
                    .Select(g => new { g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).Take(8).ToListAsync())
                .Select(x => new ChartPoint(x.Key, x.Count)).ToList();

            vm.ReportsByReason = (await _context.PropertyReports.GroupBy(r => r.Reason)
                    .Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .OrderByDescending(x => x.Count).Select(x => new ChartPoint(x.Key.ToString(), x.Count)).ToList();

            return View(vm);
        }

        private static List<ChartPoint> LastTwelveMonths(DateTime since, Func<int, int, decimal> valueFor)
        {
            var points = new List<ChartPoint>();
            for (int i = 0; i < 12; i++)
            {
                var month = since.AddMonths(i);
                points.Add(new ChartPoint(
                    month.ToString("MMM yy", CultureInfo.InvariantCulture),
                    valueFor(month.Year, month.Month)));
            }
            return points;
        }
    }
}
