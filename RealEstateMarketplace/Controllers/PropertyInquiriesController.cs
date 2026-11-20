
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class PropertyInquiriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PropertyInquiriesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int propertyListingId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var listing = await _context.PropertyListings
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l =>
                    l.Id == propertyListingId &&
                    l.Status == ListingStatus.Approved &&
                    !l.IsDeleted);

            if (listing == null)
                return NotFound();

            // Owners cannot request their own properties.
            if (listing.Property.OwnerId == userId)
                return Forbid();

            // Prevent duplicate pending requests.
            bool alreadyRequested = await _context.PropertyInquiries
                .AnyAsync(i =>
                    i.PropertyListingId == propertyListingId &&
                    i.RequesterId == userId &&
                    i.Status == PropertyInquiryStatus.Pending);

            if (alreadyRequested)
            {
                TempData["Error"] =
                    "You already have a pending request for this property.";

                return RedirectToAction(
                    "Details", "Properties",
                    new { id = propertyListingId });
            }

            var inquiry = new PropertyInquiry
            {
                PropertyListingId = listing.Id,
                RequesterId = userId,
                OwnerId = listing.Property.OwnerId,
                RequestType = listing.ListingType,
                Status = PropertyInquiryStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.PropertyInquiries.Add(inquiry);
            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Your request was submitted successfully!";

            return RedirectToAction(
                "Details", "Properties",
                new { id = propertyListingId });
        }


        [HttpGet]
        public async Task<IActionResult> Incoming()
        {
            var ownerId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(ownerId))
                return Challenge();

            var inquiries = await _context.PropertyInquiries
                .AsNoTracking()
                .Include(i => i.PropertyListing)
                .Include(i => i.Requester)
                .Where(i => i.OwnerId == ownerId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(inquiries);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var ownerId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(ownerId))
                return Challenge();

            var inquiry = await _context.PropertyInquiries
                .Include(i => i.PropertyListing)
                .ThenInclude(l => l.Property)
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.OwnerId == ownerId);

            if (inquiry == null)
                return NotFound();

            if (inquiry.Status != PropertyInquiryStatus.Pending)
            {
                TempData["Error"] =
                    "This request has already been processed.";

                return RedirectToAction(nameof(Incoming));
            }

            if (inquiry.PropertyListing.IsDeleted ||
                inquiry.PropertyListing.Status != ListingStatus.Approved ||
                inquiry.PropertyListing.Property.OwnerId != ownerId)
            {
                TempData["Error"] =
                    "This property is no longer available for this request.";

                return RedirectToAction(nameof(Incoming));
            }

            bool alreadyAccepted = await _context.PropertyInquiries
    .AnyAsync(i =>
        i.PropertyListingId == inquiry.PropertyListingId &&
        i.Id != inquiry.Id &&
        i.Status == PropertyInquiryStatus.Accepted);

            if (alreadyAccepted)
            {
                TempData["Error"] =
                    "Another request has already been accepted for this property.";

                return RedirectToAction(nameof(Incoming));
            }

            inquiry.Status = PropertyInquiryStatus.Accepted;
            inquiry.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Property request accepted successfully.";

            return RedirectToAction(nameof(Incoming));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var ownerId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(ownerId))
                return Challenge();

            var inquiry = await _context.PropertyInquiries
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.OwnerId == ownerId);

            if (inquiry == null)
                return NotFound();

            if (inquiry.Status != PropertyInquiryStatus.Pending)
            {
                TempData["Error"] =
                    "This request has already been processed.";

                return RedirectToAction(nameof(Incoming));
            }

            inquiry.Status = PropertyInquiryStatus.Rejected;
            inquiry.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Property request rejected successfully.";

            return RedirectToAction(nameof(Incoming));
        }


        [HttpGet]
        public async Task<IActionResult> MyRequests()
        {
            var requesterId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(requesterId))
            {
                return Challenge();
            }

            var inquiries = await _context.PropertyInquiries
                .AsNoTracking()
                .Include(i => i.PropertyListing)
                .Include(i => i.Owner)
                .Where(i => i.RequesterId == requesterId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return View(inquiries);
        }

    }
}
