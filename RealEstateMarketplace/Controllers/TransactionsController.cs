
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

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TransactionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
    }
}
