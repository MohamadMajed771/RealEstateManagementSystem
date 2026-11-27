using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Areas.Admin.Controllers
{
    /// <summary>Promotion payments for financial reporting.</summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Index(PaymentStatus? status, DateTime? from, DateTime? to)
        {
            var query = _context.Payments.AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.ListingPromotion).ThenInclude(lp => lp.PropertyListing)
                .Include(p => p.ListingPromotion).ThenInclude(lp => lp.PromotionPlan)
                .AsQueryable();

            if (status != null) query = query.Where(p => p.Status == status);
            if (from != null) query = query.Where(p => p.CreatedAt >= from.Value.ToUniversalTime());
            if (to != null) query = query.Where(p => p.CreatedAt < to.Value.ToUniversalTime().AddDays(1));

            var payments = await query.OrderByDescending(p => p.CreatedAt).Take(1000).ToListAsync();

            ViewBag.Status = status;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Revenue = payments.Where(p => p.Status == PaymentStatus.Succeeded).Sum(p => p.Amount);
            ViewBag.ActivePromotions = await _context.ListingPromotions
                .CountAsync(p => p.Status == ListingPromotionStatus.Active && p.EndDate > DateTime.UtcNow);

            return View(payments);
        }
    }
}
