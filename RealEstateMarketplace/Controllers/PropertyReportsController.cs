using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Controllers
{
    /// <summary>
    /// Users report listings that do not match reality.
    /// Reports are reviewed in Admin > Reports.
    /// </summary>
    [Authorize]
    public class PropertyReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PropertyReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int propertyListingId, PropertyReportReason reason, string? description)
        {
            var userId = _userManager.GetUserId(User)!;
            var back = RedirectToAction("Details", "Properties", new { id = propertyListingId });

            var listing = await _context.PropertyListings
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.Id == propertyListingId && !l.IsDeleted);

            if (listing == null) return NotFound();

            if (listing.Property.OwnerId == userId)
            {
                TempData["Error"] = "You cannot report your own listing.";
                return back;
            }

            description = description?.Trim();
            if (string.IsNullOrEmpty(description) || description.Length < 10 || description.Length > 2000 ||
                !Enum.IsDefined(reason))
            {
                TempData["Error"] = "Please choose a reason and describe the problem (10–2000 characters).";
                return back;
            }

            bool alreadyOpen = await _context.PropertyReports.AnyAsync(r =>
                r.PropertyListingId == propertyListingId &&
                r.ReportedByUserId == userId &&
                (r.Status == PropertyReportStatus.Pending || r.Status == PropertyReportStatus.UnderReview));

            if (alreadyOpen)
            {
                TempData["Error"] = "You already reported this property. An administrator is reviewing it.";
                return back;
            }

            _context.PropertyReports.Add(new PropertyReport
            {
                PropertyListingId = propertyListingId,
                ReportedByUserId = userId,
                Reason = reason,
                Description = description,
                Status = PropertyReportStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thank you. Your report was sent to the marketplace administrators.";
            return back;
        }
    }
}
