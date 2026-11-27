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
    /// Property reports: investigate and record a moderation decision.
    /// Actions: dismiss, request corrections, hide, remove, suspend owner.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : Controller
    {
        public enum ReportAction
        {
            Dismiss = 1,
            RequestCorrections = 2,
            HideListing = 3,
            RemoveListing = 4,
            SuspendOwner = 5
        }

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserModerationService _moderation;

        public ReportsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            UserModerationService moderation)
        {
            _context = context;
            _userManager = userManager;
            _moderation = moderation;
        }

        [HttpGet]
        public async Task<IActionResult> Index(bool showClosed = false)
        {
            var reports = await _context.PropertyReports
                .AsNoTracking()
                .Include(r => r.PropertyListing).ThenInclude(l => l.Property).ThenInclude(p => p.Owner)
                .Include(r => r.ReportedByUser)
                .Include(r => r.ReviewedByUser)
                .Where(r => showClosed ||
                            r.Status == PropertyReportStatus.Pending ||
                            r.Status == PropertyReportStatus.UnderReview)
                .OrderBy(r => r.Status)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            // How many reports each listing has in total
            var listingIds = reports.Select(r => r.PropertyListingId).Distinct().ToList();
            ViewBag.ReportCounts = await _context.PropertyReports
                .Where(r => listingIds.Contains(r.PropertyListingId))
                .GroupBy(r => r.PropertyListingId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            ViewBag.ShowClosed = showClosed;
            return View(reports);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Investigate(int id)
        {
            var report = await _context.PropertyReports.FindAsync(id);
            if (report == null) return NotFound();

            if (report.Status == PropertyReportStatus.Pending)
            {
                report.Status = PropertyReportStatus.UnderReview;
                report.ReviewedByUserId = _userManager.GetUserId(User);
                report.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id, ReportAction decision, string? notes)
        {
            var adminId = _userManager.GetUserId(User)!;
            notes = notes?.Trim();

            var report = await _context.PropertyReports
                .Include(r => r.PropertyListing).ThenInclude(l => l.Property).ThenInclude(p => p.Owner)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null) return NotFound();

            if (report.Status is PropertyReportStatus.Resolved or PropertyReportStatus.Dismissed)
            {
                TempData["Error"] = "This report is already closed.";
                return RedirectToAction(nameof(Index));
            }

            if (decision != ReportAction.Dismiss && string.IsNullOrWhiteSpace(notes))
            {
                TempData["Error"] = "Please write a note explaining the decision.";
                return RedirectToAction(nameof(Index));
            }

            var listing = report.PropertyListing;
            var owner = listing.Property.Owner;
            var now = DateTime.UtcNow;

            switch (decision)
            {
                case ReportAction.Dismiss:
                    report.Status = PropertyReportStatus.Dismissed;
                    break;

                case ReportAction.RequestCorrections:
                    // Owner must edit the listing; editing sends it back to moderation.
                    listing.Status = ListingStatus.Rejected;
                    listing.PublishedAt = null;
                    listing.UpdatedAt = now;
                    AddModeration(listing.Id, adminId, ModerationDecision.RequestedChanges, notes);
                    Notify(owner.Id, "Corrections requested for your listing",
                        $"After a user report, an administrator asked you to correct '{listing.Title}': {notes}. " +
                        "Edit the listing to submit it again.", NotificationType.ListingRejected, listing.Id,
                        $"/PropertyListings/Edit/{listing.Id}");
                    report.Status = PropertyReportStatus.Resolved;
                    break;

                case ReportAction.HideListing:
                    listing.Status = ListingStatus.Archived;
                    listing.UpdatedAt = now;
                    AddModeration(listing.Id, adminId, ModerationDecision.Rejected, "Hidden after report: " + notes);
                    Notify(owner.Id, "Your listing was hidden",
                        $"'{listing.Title}' was hidden from the marketplace after a report. Reason: {notes}",
                        NotificationType.ListingRejected, listing.Id, "/PropertyListings/MyListings");
                    report.Status = PropertyReportStatus.Resolved;
                    break;

                case ReportAction.RemoveListing:
                    listing.IsDeleted = true;
                    listing.UpdatedAt = now;
                    AddModeration(listing.Id, adminId, ModerationDecision.Rejected, "Removed after report: " + notes);
                    Notify(owner.Id, "Your listing was removed",
                        $"'{listing.Title}' was removed from the marketplace. Reason: {notes}",
                        NotificationType.ListingRejected, listing.Id, "/Notifications");
                    report.Status = PropertyReportStatus.Resolved;
                    break;

                case ReportAction.SuspendOwner:
                    await _moderation.SuspendAsync(owner, adminId, $"Suspended after report #{report.Id}: {notes}");
                    report.Status = PropertyReportStatus.Resolved;
                    break;

                default:
                    return BadRequest();
            }

            report.ModeratorNotes = notes;
            report.ReviewedByUserId = adminId;
            report.ReviewedAt = now;
            report.UpdatedAt = now;

            Notify(report.ReportedByUserId, "Your report was reviewed",
                decision == ReportAction.Dismiss
                    ? $"We checked '{listing.Title}' and found the listing valid."
                    : $"Thank you. We took action on '{listing.Title}'.",
                NotificationType.SystemAnnouncement, listing.Id, "/Notifications");

            _moderation.Audit(adminId, $"Report{decision}", "PropertyReport", report.Id.ToString(),
                $"Listing {listing.Id}: {decision}. {notes}");

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Report #{report.Id}: {decision} recorded.";
            return RedirectToAction(nameof(Index));
        }

        private void AddModeration(int listingId, string adminId, ModerationDecision decision, string? reason)
        {
            _context.ListingModerations.Add(new ListingModeration
            {
                PropertyListingId = listingId,
                ModeratorId = adminId,
                Decision = decision,
                Reason = reason,
                ReviewedAt = DateTime.UtcNow
            });
        }

        private void Notify(string userId, string title, string message, NotificationType type, int listingId, string url)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title.Length > 150 ? title[..150] : title,
                Message = message.Length > 1000 ? message[..1000] : message,
                Type = type,
                RelatedEntityId = listingId,
                RelatedEntityType = "PropertyListing",
                ActionUrl = url,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
