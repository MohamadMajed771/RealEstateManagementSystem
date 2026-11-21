
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
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

        // ==========================================
        // CREATE PROPERTY REQUEST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int propertyListingId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var listing = await _context.PropertyListings
                    .Include(l => l.Property)
                    .FirstOrDefaultAsync(l =>
                        l.Id == propertyListingId);

                if (listing == null ||
                    listing.IsDeleted ||
                    listing.Status != ListingStatus.Approved)
                {
                    await dbTransaction.RollbackAsync();

                    TempData["Error"] =
                        "This property is not available for requests.";

                    return RedirectToAction(
                        "Index", "Properties");
                }

                if (listing.Property.OwnerId == userId)
                {
                    await dbTransaction.RollbackAsync();
                    return Forbid();
                }

                // A listing is reserved while a transaction
                // is pending or has been confirmed.
                bool hasOngoingTransaction =
                    await _context.Transactions.AnyAsync(t =>
                        t.PropertyListingId == propertyListingId &&
                        t.Status != TransactionStatus.Completed &&
                        t.Status != TransactionStatus.Cancelled &&
                        t.Status != TransactionStatus.Rejected);

                if (hasOngoingTransaction)
                {
                    await dbTransaction.RollbackAsync();

                    TempData["Error"] =
                        "This property already has an ongoing transaction.";

                    return RedirectToAction(
                        "Details", "Properties",
                        new { id = propertyListingId });
                }

                bool alreadyRequested =
                    await _context.PropertyInquiries.AnyAsync(i =>
                        i.PropertyListingId == propertyListingId &&
                        i.RequesterId == userId &&
                        i.Status == PropertyInquiryStatus.Pending);

                if (alreadyRequested)
                {
                    await dbTransaction.RollbackAsync();

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
                await dbTransaction.CommitAsync();

                TempData["Success"] =
                    "Your request was submitted successfully!";

                return RedirectToAction(
                    "Details", "Properties",
                    new { id = propertyListingId });
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to submit your request. Please try again.";

                return RedirectToAction(
                    "Details", "Properties",
                    new { id = propertyListingId });
            }
        }

        // ==========================================
        // INCOMING REQUESTS
        // ==========================================

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

        // ==========================================
        // ACCEPT PROPERTY REQUEST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var ownerId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(ownerId))
                return Challenge();

            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var inquiry = await _context.PropertyInquiries
                    .Include(i => i.PropertyListing)
                    .ThenInclude(l => l.Property)
                    .FirstOrDefaultAsync(i =>
                        i.Id == id &&
                        i.OwnerId == ownerId);

                if (inquiry == null)
                {
                    await dbTransaction.RollbackAsync();
                    return NotFound();
                }

                if (inquiry.Status != PropertyInquiryStatus.Pending)
                {
                    await dbTransaction.RollbackAsync();

                    TempData["Error"] =
                        "This request has already been processed.";

                    return RedirectToAction(nameof(Incoming));
                }

                var listing = inquiry.PropertyListing;

                if (listing.IsDeleted ||
                    listing.Status != ListingStatus.Approved ||
                    listing.Property.OwnerId != ownerId ||
                    listing.ListingType != inquiry.RequestType)
                {
                    await dbTransaction.RollbackAsync();

                    TempData["Error"] =
                        "This property is no longer available.";

                    return RedirectToAction(nameof(Incoming));
                }

                // Prevent another ongoing transaction
                // for the same property listing.
                bool hasOngoingTransaction =
                    await _context.Transactions.AnyAsync(t =>
                        t.PropertyListingId == listing.Id &&
                        t.Status != TransactionStatus.Completed &&
                        t.Status != TransactionStatus.Cancelled &&
                        t.Status != TransactionStatus.Rejected);

                if (hasOngoingTransaction)
                {
                    await dbTransaction.RollbackAsync();

                    TempData["Error"] =
                        "This property already has an ongoing transaction.";

                    return RedirectToAction(nameof(Incoming));
                }

                // Prevent accepting another request
                // while an earlier accepted request is
                // still awaiting completion.
                bool hasAcceptedInquiry =
                    await _context.PropertyInquiries.AnyAsync(i =>
                        i.PropertyListingId == listing.Id &&
                        i.Id != inquiry.Id &&
                        i.Status == PropertyInquiryStatus.Accepted &&
                        _context.Transactions.Any(t =>
                            t.PropertyInquiryId == i.Id &&
                            t.Status != TransactionStatus.Completed &&
                            t.Status != TransactionStatus.Cancelled &&
                            t.Status != TransactionStatus.Rejected));

                if (hasAcceptedInquiry)
                {
                    await dbTransaction.RollbackAsync();

                    TempData["Error"] =
                        "Another request has already been accepted.";

                    return RedirectToAction(nameof(Incoming));
                }

                // Prevent duplicate transactions
                // for this specific inquiry.
                bool inquiryHasTransaction =
                    await _context.Transactions.AnyAsync(t =>
                        t.PropertyInquiryId == inquiry.Id);

                if (inquiryHasTransaction)
                {
                    await dbTransaction.RollbackAsync();

                    TempData["Error"] =
                        "A transaction already exists for this request.";

                    return RedirectToAction(nameof(Incoming));
                }

                if (listing.Price <= 0)
                {
                    await dbTransaction.RollbackAsync();

                    TempData["Error"] =
                        "The listing price must be greater than zero.";

                    return RedirectToAction(nameof(Incoming));
                }

                var now = DateTime.UtcNow;

                var transaction = new Transaction
                {
                    PropertyInquiryId = inquiry.Id,
                    PropertyListingId = listing.Id,
                    TransactionType = inquiry.RequestType,
                    BuyerId = inquiry.RequesterId,
                    SellerId = ownerId,
                    AgreedPrice = listing.Price,
                    Currency = listing.Currency,
                    Status = TransactionStatus.PendingConfirmation,
                    BuyerConfirmed = false,
                    SellerConfirmed = false,
                    CreatedAt = now
                };

                inquiry.Status = PropertyInquiryStatus.Accepted;
                inquiry.RespondedAt = now;

                _context.Transactions.Add(transaction);

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                TempData["Success"] =
                    "Property request accepted and transaction created successfully.";

                return RedirectToAction(nameof(Incoming));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sqlEx &&
                      (sqlEx.Number == 2601 ||
                       sqlEx.Number == 2627))
            {
                await dbTransaction.RollbackAsync();

                TempData["Error"] =
                    "A conflicting transaction or request already exists.";

                return RedirectToAction(nameof(Incoming));
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to accept the request. Please try again.";

                return RedirectToAction(nameof(Incoming));
            }
        }

        // ==========================================
        // REJECT PROPERTY REQUEST
        // ==========================================

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

        // ==========================================
        // MY REQUESTS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> MyRequests()
        {
            var requesterId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(requesterId))
                return Challenge();

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
