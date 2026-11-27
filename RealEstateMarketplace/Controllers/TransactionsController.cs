
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services.Messaging;
using SharedContracts.Events;

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public TransactionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
        }

        // ==========================================
        // MY TRANSACTIONS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> MyTransactions()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var transactions = await _context.Transactions
                .AsNoTracking()
                .Include(t => t.PropertyListing)
                .Include(t => t.Buyer)
                .Include(t => t.Seller)
                .Where(t =>
                    t.BuyerId == userId ||
                    t.SellerId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var transactionIds = transactions
                .Select(t => t.Id)
                .ToList();

            // A transaction can now have multiple rental
            // proposals because rejected proposals are
            // preserved for negotiation history.
            var allAgreements = await _context.RentalAgreements
                .AsNoTracking()
                .Where(a => transactionIds.Contains(a.TransactionId))
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            // Select the latest proposal for each transaction.
            // This avoids duplicate dictionary keys.
            var latestAgreements = allAgreements
                .GroupBy(a => a.TransactionId)
                .ToDictionary(
                    group => group.Key,
                    group => group.First());

            ViewBag.RentalAgreements = latestAgreements;

            // Latest ownership-transfer request per transaction.
            var transferRequests = await _context.TransferRequests
                .AsNoTracking()
                .Where(r => transactionIds.Contains(r.TransactionId))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.TransferRequests = transferRequests
                .GroupBy(r => r.TransactionId)
                .ToDictionary(g => g.Key, g => g.First());

            ViewBag.TransferServiceUrl =
                _configuration["TransferService:BaseUrl"];

            ViewBag.ReviewedTransactionIds = (await _context.PropertyReviews
                .Where(r => transactionIds.Contains(r.TransactionId))
                .Select(r => r.TransactionId)
                .ToListAsync()).ToHashSet();

            return View(transactions);
        }

        // ==========================================
        // CONFIRM TRANSACTION
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
                return NotFound();

            bool isBuyer = transaction.BuyerId == userId;
            bool isSeller = transaction.SellerId == userId;

            if (!isBuyer && !isSeller)
                return Forbid();

            if (transaction.Status !=
                TransactionStatus.PendingConfirmation)
            {
                TempData["Error"] =
                    "This transaction is not awaiting confirmation.";

                return RedirectToAction(nameof(MyTransactions));
            }

            var now = DateTime.UtcNow;

            if (isBuyer)
            {
                if (transaction.BuyerConfirmed)
                {
                    TempData["Error"] =
                        "You have already confirmed this transaction.";

                    return RedirectToAction(nameof(MyTransactions));
                }

                transaction.BuyerConfirmed = true;
                transaction.BuyerConfirmedAt = now;
            }
            else
            {
                if (transaction.SellerConfirmed)
                {
                    TempData["Error"] =
                        "You have already confirmed this transaction.";

                    return RedirectToAction(nameof(MyTransactions));
                }

                transaction.SellerConfirmed = true;
                transaction.SellerConfirmedAt = now;
            }

            if (transaction.BuyerConfirmed &&
                transaction.SellerConfirmed)
            {
                transaction.Status = TransactionStatus.Confirmed;
            }

            transaction.UpdatedAt = now;

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            TempData["Success"] =
                transaction.Status == TransactionStatus.Confirmed
                    ? "Both parties confirmed. Transaction confirmed successfully!"
                    : "Your confirmation was saved. Waiting for the other party.";

            return RedirectToAction(nameof(MyTransactions));
        }

        // ==========================================
        // REQUEST OWNERSHIP TRANSFER (SALE ONLY)
        // ==========================================
        //
        // Saves the TransferRequest and a TransferRequested
        // outbox message in the same database transaction.
        // OutboxPublisherService then sends it to RabbitMQ.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestTransfer(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var transaction = await _context.Transactions
                .Include(t => t.PropertyListing)
                    .ThenInclude(l => l.Property)
                .Include(t => t.Buyer)
                .Include(t => t.Seller)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
                return NotFound();

            if (transaction.BuyerId != userId)
                return Forbid();

            if (transaction.TransactionType != ListingType.Sale)
            {
                TempData["Error"] =
                    "Ownership transfer is only available for sales.";
                return RedirectToAction(nameof(MyTransactions));
            }

            if (transaction.Status != TransactionStatus.Confirmed ||
                !transaction.BuyerConfirmed ||
                !transaction.SellerConfirmed)
            {
                TempData["Error"] =
                    "Both buyer and seller must confirm the sale first.";
                return RedirectToAction(nameof(MyTransactions));
            }

            bool alreadyRequested = await _context.TransferRequests
                .AnyAsync(r =>
                    r.TransactionId == id &&
                    r.Status != TransferRequestStatus.Cancelled &&
                    r.Status != TransferRequestStatus.Rejected);

            if (alreadyRequested)
            {
                TempData["Error"] =
                    "An ownership-transfer request already exists for this sale.";
                return RedirectToAction(nameof(MyTransactions));
            }

            var now = DateTime.UtcNow;
            var property = transaction.PropertyListing.Property;

            var request = new TransferRequest
            {
                TransactionId = transaction.Id,
                Status = TransferRequestStatus.Sent,
                CreatedAt = now,
                SentAt = now
            };

            transaction.Status = TransactionStatus.TransferRequested;
            transaction.TransferRequestId = request.Id;
            transaction.TransferRequestedAt = now;
            transaction.UpdatedAt = now;

            _context.TransferRequests.Add(request);

            // Only the information System 2 needs.
            Outbox.Add(_context, new TransferRequested
            {
                TransferRequestId = request.Id,
                MarketplaceTransactionId = transaction.Id,
                MarketplacePropertyListingId = transaction.PropertyListingId,
                PropertyTitle = transaction.PropertyListing.Title,
                PropertyType = property.Type.ToString(),
                Country = property.Country,
                City = property.City,
                Area = property.Area,
                StreetAddress = property.StreetAddress,
                SizeSquareMeters = property.SizeSquareMeters,
                AgreedPrice = transaction.AgreedPrice,
                Currency = transaction.Currency,
                Buyer = ToParty(transaction.Buyer),
                Seller = ToParty(transaction.Seller)
            });

            _context.Notifications.Add(new Notification
            {
                UserId = transaction.SellerId,
                Title = "Ownership transfer requested",
                Message =
                    $"The buyer requested ownership-transfer assistance for " +
                    $"'{transaction.PropertyListing.Title}'. You will be notified " +
                    "when the Transfer Service opens the case.",
                Type = NotificationType.TransferStatusChanged,
                RelatedEntityId = transaction.Id,
                RelatedEntityType = "Transaction",
                ActionUrl = "/Transactions/MyTransactions",
                CreatedAt = now
            });

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Ownership-transfer request sent. You will be notified " +
                "when the Transfer Service creates your case.";

            return RedirectToAction(nameof(MyTransactions));
        }

        // ==========================================
        // CANCEL OWNERSHIP TRANSFER (BUYER OR SELLER)
        // ==========================================
        //
        // Sends TransferCancelled to System 2 (through the outbox). When System 2
        // confirms, the sale goes back to "Confirmed" (see TransferUpdatesConsumer).

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTransfer(int id, string? reason)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var transaction = await _context.Transactions
                .Include(t => t.PropertyListing)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
                return NotFound();

            if (transaction.BuyerId != userId && transaction.SellerId != userId)
                return Forbid();

            reason = reason?.Trim();
            if (string.IsNullOrEmpty(reason))
            {
                TempData["Error"] = "Please give a reason for cancelling the transfer request.";
                return RedirectToAction(nameof(MyTransactions));
            }
            if (reason.Length > 500) reason = reason[..500];

            var request = await _context.TransferRequests
                .Where(r => r.TransactionId == id)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (request == null ||
                request.Status is not (TransferRequestStatus.Sent or TransferRequestStatus.Accepted
                    or TransferRequestStatus.InProgress))
            {
                TempData["Error"] = "There is no active ownership-transfer request to cancel.";
                return RedirectToAction(nameof(MyTransactions));
            }

            // Once the officer sent the file for official processing, only the
            // Transfer Service can stop it (avoids a "completed" after "cancelled").
            if (request.ExternalStatus is "ReadyForProcessing" or "ExternalProcessingInProgress")
            {
                TempData["Error"] = "The case is already in official processing. Contact your case officer to stop it.";
                return RedirectToAction(nameof(MyTransactions));
            }

            var now = DateTime.UtcNow;
            var who = transaction.BuyerId == userId ? "buyer" : "seller";

            // The Transfer Service decides: it confirms (TransferStatusChanged "Cancelled")
            // or refuses (TransferCancellationRefused) when official processing has started.
            // Until then the request waits, so both systems always agree.
            request.Status = TransferRequestStatus.CancellationRequested;
            request.UpdatedAt = now;

            Outbox.Add(_context, new TransferCancelled
            {
                TransferRequestId = request.Id,
                Reason = $"Cancelled by the {who}: {reason}"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cancellation sent to the Transfer Service. You will be notified when it is confirmed.";
            return RedirectToAction(nameof(MyTransactions));
        }

        private static TransferParty ToParty(ApplicationUser user) => new()
        {
            MarketplaceUserId = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber
        };
    }
}
