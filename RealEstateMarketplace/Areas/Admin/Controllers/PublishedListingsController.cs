using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

namespace RealEstateMarketplace.Areas.Admin.Controllers
{
    /// <summary>
    /// All listings (published, hidden, sold...) with hide / remove / restore.
    /// Pending listings are approved in ListingsController.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PublishedListingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserModerationService _moderation;

        public PublishedListingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            UserModerationService moderation)
        {
            _context = context;
            _userManager = userManager;
            _moderation = moderation;
        }

        [HttpGet]
        public async Task<IActionResult> Index(ListingStatus? status = ListingStatus.Approved, string? q = null, bool removed = false)
        {
            var query = _context.PropertyListings.AsNoTracking()
                .Include(l => l.Property).ThenInclude(p => p.Owner)
                .Where(l => l.IsDeleted == removed);

            if (status != null && !removed) query = query.Where(l => l.Status == status);
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(l => l.Title.Contains(q) || l.Property.City.Contains(q) || l.Property.Owner.Email!.Contains(q));

            ViewBag.Status = status;
            ViewBag.Query = q;
            ViewBag.Removed = removed;
            ViewBag.ReportCounts = await _context.PropertyReports
                .GroupBy(r => r.PropertyListingId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return View(await query.OrderByDescending(l => l.CreatedAt).Take(500).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hide(int id, string? reason) =>
            await ChangeAsync(id, reason, l => l.Status = ListingStatus.Archived, "hidden from the marketplace", "ListingHidden");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id, string? reason) =>
            await ChangeAsync(id, reason, l => l.IsDeleted = true, "removed from the marketplace", "ListingRemoved");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id) =>
            await ChangeAsync(id, "Restored by an administrator.", l =>
            {
                l.IsDeleted = false;
                l.Status = ListingStatus.Approved;
                l.PublishedAt ??= DateTime.UtcNow;
            }, "restored and is visible again", "ListingRestored");

        private async Task<IActionResult> ChangeAsync(int id, string? reason, Action<PropertyListing> apply,
            string whatHappened, string auditAction)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please give a reason.";
                return RedirectToAction(nameof(Index));
            }

            var listing = await _context.PropertyListings.Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.Id == id);
            if (listing == null) return NotFound();

            apply(listing);
            listing.UpdatedAt = DateTime.UtcNow;

            var adminId = _userManager.GetUserId(User)!;
            _context.ListingModerations.Add(new ListingModeration
            {
                PropertyListingId = listing.Id,
                ModeratorId = adminId,
                Decision = auditAction == "ListingRestored" ? ModerationDecision.Approved : ModerationDecision.Rejected,
                Reason = reason.Trim(),
                ReviewedAt = DateTime.UtcNow
            });
            _context.Notifications.Add(new Notification
            {
                UserId = listing.Property.OwnerId,
                Title = $"Your listing was {whatHappened.Split(' ')[0]}",
                Message = $"'{listing.Title}' was {whatHappened}. Reason: {reason.Trim()}",
                Type = auditAction == "ListingRestored" ? NotificationType.ListingApproved : NotificationType.ListingRejected,
                RelatedEntityId = listing.Id,
                RelatedEntityType = "PropertyListing",
                ActionUrl = "/PropertyListings/MyListings",
                CreatedAt = DateTime.UtcNow
            });
            _moderation.Audit(adminId, auditAction, "PropertyListing", listing.Id.ToString(), reason.Trim());

            await _context.SaveChangesAsync();
            TempData["Success"] = $"'{listing.Title}' was {whatHappened}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
