
using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class RentalAgreementsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RentalAgreementsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // VIEW RENTAL AGREEMENT
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var agreement = await _context.RentalAgreements
                .AsNoTracking()
                .Include(a => a.Transaction)
                    .ThenInclude(t => t.PropertyListing)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (agreement == null)
                return NotFound();

            if (agreement.Transaction.BuyerId != userId &&
                agreement.Transaction.SellerId != userId)
                return Forbid();

            return View(agreement);
        }

        // ==========================================
        // LANDLORD PROPOSES RENTAL AGREEMENT
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Propose(
            int transactionId,
            DateTime startDate,
            DateTime endDate,
            decimal monthlyRent)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var today = DateTime.UtcNow.Date;

            if (startDate.Date < today ||
                endDate.Date <= startDate.Date ||
                monthlyRent <= 0 ||
                monthlyRent > 999999999999.99m)
            {
                TempData["Error"] =
                    "Enter valid rental dates and a valid monthly rent.";

                return RedirectToAction(
                    "MyTransactions", "Transactions");
            }

            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.PropertyListing)
                        .ThenInclude(l => l.Property)
                    .FirstOrDefaultAsync(t => t.Id == transactionId);

                if (transaction == null)
                    return NotFound();

                if (transaction.SellerId != userId ||
                    transaction.PropertyListing.Property.OwnerId != userId)
                    return Forbid();

                if (transaction.TransactionType != ListingType.Rent ||
                    transaction.Status != TransactionStatus.Confirmed ||
                    !transaction.BuyerConfirmed ||
                    !transaction.SellerConfirmed ||
                    transaction.PropertyListing.IsDeleted ||
                    transaction.PropertyListing.Status != ListingStatus.Approved)
                {
                    TempData["Error"] =
                        "This transaction is not ready for a rental agreement.";

                    return RedirectToAction(
                        "MyTransactions", "Transactions");
                }

                // Check only ongoing agreements.
                // Rejected proposals do not block revisions.
                bool ongoingAgreementExists =
                    await _context.RentalAgreements.AnyAsync(a =>
                        a.TransactionId == transactionId &&
                        (a.Status == RentalAgreementStatus.Draft ||
                         a.Status == RentalAgreementStatus.PendingApproval ||
                         a.Status == RentalAgreementStatus.Scheduled ||
                         a.Status == RentalAgreementStatus.Active));

                if (ongoingAgreementExists)
                {
                    TempData["Error"] =
                        "This transaction already has an ongoing rental agreement.";

                    return RedirectToAction(
                        "MyTransactions", "Transactions");
                }

                // A finalized agreement must never be replaced
                // by a new proposal in this negotiation flow.
                bool finalizedAgreementExists =
                    await _context.RentalAgreements.AnyAsync(a =>
                        a.TransactionId == transactionId &&
                        a.FinalizedAt != null);

                if (finalizedAgreementExists)
                {
                    TempData["Error"] =
                        "This transaction already has a finalized rental agreement.";

                    return RedirectToAction(
                        "MyTransactions", "Transactions");
                }

                var now = DateTime.UtcNow;

                var agreement = new RentalAgreement
                {
                    TransactionId = transactionId,
                    StartDate = startDate.Date,
                    EndDate = endDate.Date,
                    MonthlyRent = monthlyRent,
                    Currency = transaction.Currency,

                    Status = RentalAgreementStatus.PendingApproval,

                    LandlordApproved = true,
                    LandlordApprovedAt = now,

                    TenantApproved = false,
                    TenantApprovedAt = null,

                    CreatedAt = now
                };

                _context.RentalAgreements.Add(agreement);

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                TempData["Success"] =
                    "Rental proposal submitted. Waiting for tenant approval.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = agreement.Id });
            }
            catch (DbUpdateException)
            {
                await dbTransaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to create the rental agreement. " +
                    "An ongoing agreement may already exist.";

                return RedirectToAction(
                    "MyTransactions", "Transactions");
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();

                TempData["Error"] =
                    "An error occurred while creating the rental agreement.";

                return RedirectToAction(
                    "MyTransactions", "Transactions");
            }
        }

        // ==========================================
        // APPROVE RENTAL AGREEMENT
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var agreement = await _context.RentalAgreements
                    .Include(a => a.Transaction)
                        .ThenInclude(t => t.PropertyListing)
                            .ThenInclude(l => l.Property)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (agreement == null)
                    return NotFound();

                var transaction = agreement.Transaction;
                var listing = transaction.PropertyListing;

                bool isTenant = transaction.BuyerId == userId;

                bool isLandlord =
                    transaction.SellerId == userId &&
                    listing.Property.OwnerId == userId;

                if (!isTenant && !isLandlord)
                    return Forbid();

                if (transaction.TransactionType != ListingType.Rent ||
                    transaction.Status != TransactionStatus.Confirmed ||
                    !transaction.BuyerConfirmed ||
                    !transaction.SellerConfirmed ||
                    agreement.Status != RentalAgreementStatus.PendingApproval ||
                    listing.IsDeleted ||
                    listing.Status != ListingStatus.Approved)
                {
                    TempData["Error"] =
                        "This rental agreement cannot be approved.";

                    return RedirectToAction(
                        nameof(Details), new { id });
                }

                var today = DateTime.UtcNow.Date;

                if (agreement.StartDate.Date < today ||
                    agreement.EndDate.Date <= agreement.StartDate.Date)
                {
                    TempData["Error"] =
                        "The proposed rental dates are no longer valid. " +
                        "The agreement must be revised.";

                    return RedirectToAction(
                        nameof(Details), new { id });
                }

                var now = DateTime.UtcNow;

                if (isTenant)
                {
                    if (agreement.TenantApproved)
                    {
                        TempData["Error"] =
                            "You already approved this agreement.";

                        return RedirectToAction(
                            nameof(Details), new { id });
                    }

                    agreement.TenantApproved = true;
                    agreement.TenantApprovedAt = now;
                }
                else
                {
                    if (agreement.LandlordApproved)
                    {
                        TempData["Error"] =
                            "You already approved this agreement.";

                        return RedirectToAction(
                            nameof(Details), new { id });
                    }

                    agreement.LandlordApproved = true;
                    agreement.LandlordApprovedAt = now;
                }

                if (agreement.TenantApproved &&
                    agreement.LandlordApproved)
                {
                    agreement.FinalizedAt = now;

                    // Reserve the listing immediately.
                    listing.Status = ListingStatus.Rented;
                    listing.UpdatedAt = now;

                    agreement.Status =
                        agreement.StartDate.Date == today
                            ? RentalAgreementStatus.Active
                            : RentalAgreementStatus.Scheduled;
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                TempData["Success"] =
                    agreement.Status == RentalAgreementStatus.Active
                        ? "Rental approved. The rental is now active."
                        : agreement.Status == RentalAgreementStatus.Scheduled
                            ? "Rental approved. The property is reserved."
                            : "Your approval was saved successfully.";

                return RedirectToAction(
                    nameof(Details), new { id });
            }
            catch (DbUpdateException)
            {
                await dbTransaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to save the approval. Please try again.";

                return RedirectToAction(
                    nameof(Details), new { id });
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();

                TempData["Error"] =
                    "An error occurred while approving the agreement.";

                return RedirectToAction(
                    nameof(Details), new { id });
            }
        }

        // ==========================================
        // REJECT RENTAL AGREEMENT
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            int id,
            string? rejectionReason)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["Error"] =
                    "Please explain why you are rejecting the agreement.";

                return RedirectToAction(
                    nameof(Details), new { id });
            }

            rejectionReason = rejectionReason.Trim();

            if (rejectionReason.Length > 1000)
            {
                TempData["Error"] =
                    "The rejection reason cannot exceed 1000 characters.";

                return RedirectToAction(
                    nameof(Details), new { id });
            }

            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var agreement = await _context.RentalAgreements
                    .Include(a => a.Transaction)
                        .ThenInclude(t => t.PropertyListing)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (agreement == null)
                    return NotFound();

                var transaction = agreement.Transaction;

                // Only the tenant can reject the proposal.
                if (transaction.BuyerId != userId)
                    return Forbid();

                if (transaction.TransactionType != ListingType.Rent ||
                    transaction.Status != TransactionStatus.Confirmed ||
                    agreement.Status != RentalAgreementStatus.PendingApproval ||
                    agreement.TenantApproved ||
                    agreement.FinalizedAt != null)
                {
                    TempData["Error"] =
                        "This rental agreement cannot be rejected.";

                    return RedirectToAction(
                        nameof(Details), new { id });
                }

                agreement.Status = RentalAgreementStatus.Rejected;
                agreement.RejectionReason = rejectionReason;
                agreement.RejectedByUserId = userId;
                agreement.RejectedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                TempData["Success"] =
                    "You rejected the rental proposal. " +
                    "The landlord can submit revised terms.";

                return RedirectToAction(
                    nameof(Details), new { id });
            }
            catch (DbUpdateException)
            {
                await dbTransaction.RollbackAsync();

                TempData["Error"] =
                    "Unable to reject the agreement. Please try again.";

                return RedirectToAction(
                    nameof(Details), new { id });
            }
            catch (Exception)
            {
                await dbTransaction.RollbackAsync();

                TempData["Error"] =
                    "An error occurred while rejecting the agreement.";

                return RedirectToAction(
                    nameof(Details), new { id });
            }
        }
    }
}
