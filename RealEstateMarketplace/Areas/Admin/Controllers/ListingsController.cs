
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

namespace RealEstateMarketplace.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ListingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SavedSearchNotificationService _savedSearchService;

        public ListingsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SavedSearchNotificationService savedSearchService)
        {
            _context = context;
            _userManager = userManager;
            _savedSearchService = savedSearchService;
        }

        // ==========================================
        // GET: PENDING LISTINGS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var listings = await _context.PropertyListings
                .AsNoTracking()
                .Include(l => l.Property)
                    .ThenInclude(p => p.Owner)
                .Where(l =>
                    !l.IsDeleted &&
                    l.Status == ListingStatus.PendingApproval)
                .OrderBy(l => l.CreatedAt)
                .ToListAsync();

            return View(listings);
        }

        // ==========================================
        // GET: REVIEW LISTING DETAILS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var listing = await _context.PropertyListings
                .AsNoTracking()
                .Include(l => l.Property)
                    .ThenInclude(p => p.Owner)
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    !l.IsDeleted &&
                    l.Status == ListingStatus.PendingApproval);

            if (listing == null)
                return NotFound();

            var photos = await _context.PropertyPhotos
                .AsNoTracking()
                .Where(p => p.PropertyListingId == id)
                .OrderByDescending(p => p.IsCoverPhoto)
                .ThenBy(p => p.DisplayOrder)
                .ToListAsync();

            ViewBag.Photos = photos;

            return View(listing);
        }

        // ==========================================
        // POST: APPROVE LISTING
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var admin = await _userManager.GetUserAsync(User);

            if (admin == null)
                return Challenge();

            var listing = await _context.PropertyListings
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    !l.IsDeleted &&
                    l.Status == ListingStatus.PendingApproval);

            if (listing == null)
            {
                TempData["Error"] =
                    "This listing does not exist or has already been reviewed.";

                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.UtcNow;

            // Approve and publish the listing.
            listing.Status = ListingStatus.Approved;
            listing.PublishedAt = now;
            listing.UpdatedAt = now;

            // Record the approval decision.
            _context.ListingModerations.Add(
                new ListingModeration
                {
                    PropertyListingId = listing.Id,
                    ModeratorId = admin.Id,
                    Decision = ModerationDecision.Approved,
                    Reason = "Property listing approved by Admin.",
                    ReviewedAt = now
                });

            // Notify the property owner.
            _context.Notifications.Add(
                new Notification
                {
                    UserId = listing.Property.OwnerId,
                    Title = "Your property has been approved!",
                    Message =
                        $"Your listing '{listing.Title}' has been approved " +
                        "and is now visible in the marketplace.",
                    Type = NotificationType.ListingApproved,
                    RelatedEntityId = listing.Id,
                    RelatedEntityType = "PropertyListing",
                    ActionUrl = $"/Properties/Details/{listing.Id}",
                    CreatedAt = now
                });

            await _context.SaveChangesAsync();

            // Notify matching saved-search users.
            try
            {
                await _savedSearchService.NotifyMatchingUsersAsync(
                    listing.Id, $"{Request.Scheme}://{Request.Host}");
            }
            catch (Exception)
            {
                TempData["Warning"] =
                    "The property was approved, but some saved-search " +
                    "notifications could not be sent.";
            }

            TempData["Success"] =
                $"Property '{listing.Title}' was approved successfully.";

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // POST: REJECT LISTING
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            int id,
            string reason)
        {
            // Get the logged-in Admin.
            var admin = await _userManager.GetUserAsync(User);

            if (admin == null)
                return Challenge();

            // A rejection reason is mandatory.
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] =
                    "You must enter a reason for rejecting this property.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            reason = reason.Trim();

            // The model allows a maximum of 2000 characters.
            if (reason.Length > 2000)
            {
                TempData["Error"] =
                    "The rejection reason cannot exceed 2000 characters.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            // Only pending listings can be rejected.
            var listing = await _context.PropertyListings
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    !l.IsDeleted &&
                    l.Status == ListingStatus.PendingApproval);

            if (listing == null)
            {
                TempData["Error"] =
                    "This listing does not exist or has already been reviewed.";

                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.UtcNow;

            // Change the listing status.
            listing.Status = ListingStatus.Rejected;
            listing.UpdatedAt = now;
            listing.PublishedAt = null;

            // Record the rejection decision and reason.
            _context.ListingModerations.Add(
                new ListingModeration
                {
                    PropertyListingId = listing.Id,
                    ModeratorId = admin.Id,
                    Decision = ModerationDecision.Rejected,
                    Reason = reason,
                    ReviewedAt = now
                });

            // Notify the property owner.
            _context.Notifications.Add(
                new Notification
                {
                    UserId = listing.Property.OwnerId,
                    Title = "Your property listing was rejected",
                    Message =
                        $"Your listing '{listing.Title}' was rejected. " +
                        $"Reason: {reason}",
                    Type = NotificationType.ListingRejected,
                    RelatedEntityId = listing.Id,
                    RelatedEntityType = "PropertyListing",
                    ActionUrl = "/Notifications",
                    CreatedAt = now
                });

            // Save all changes together.
            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Property '{listing.Title}' was rejected successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}
