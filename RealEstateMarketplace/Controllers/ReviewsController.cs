using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

namespace RealEstateMarketplace.Controllers
{
    public class ReviewFormViewModel
    {
        public int TransactionId { get; set; }
        public string PropertyTitle { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public bool IsRent { get; set; }

        [Range(1, 5, ErrorMessage = "Choose an overall rating from 1 to 5.")]
        [Display(Name = "Overall experience")]
        public int Rating { get; set; }

        [Range(1, 5)]
        [Display(Name = "Accuracy of the description")]
        public int? AccuracyRating { get; set; }

        [Range(1, 5)]
        [Display(Name = "Property condition")]
        public int? ConditionRating { get; set; }

        [Range(1, 5)]
        [Display(Name = "Communication with the owner")]
        public int? CommunicationRating { get; set; }

        [StringLength(2000)]
        [Display(Name = "Your review")]
        public string? Comment { get; set; }
    }

    [Authorize]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Create(int transactionId)
        {
            var (transaction, error) = await LoadEligibleAsync(transactionId);
            if (transaction == null) return error!;

            return View(new ReviewFormViewModel
            {
                TransactionId = transaction.Id,
                PropertyTitle = transaction.PropertyListing.Title,
                OwnerName = transaction.Seller.FirstName + " " + transaction.Seller.LastName,
                IsRent = transaction.TransactionType == ListingType.Rent
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReviewFormViewModel model)
        {
            var (transaction, error) = await LoadEligibleAsync(model.TransactionId);
            if (transaction == null) return error!;

            if (!ModelState.IsValid)
            {
                model.PropertyTitle = transaction.PropertyListing.Title;
                model.OwnerName = transaction.Seller.FirstName + " " + transaction.Seller.LastName;
                model.IsRent = transaction.TransactionType == ListingType.Rent;
                return View(model);
            }

            _context.PropertyReviews.Add(new PropertyReview
            {
                TransactionId = transaction.Id,
                ReviewerId = transaction.BuyerId,
                ReviewedUserId = transaction.SellerId,
                Rating = model.Rating,
                AccuracyRating = model.AccuracyRating,
                ConditionRating = model.ConditionRating,
                CommunicationRating = model.CommunicationRating,
                Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
                // Only verified parties can review, so reviews are published
                // directly; admins can hide them from Admin > Reviews.
                Status = ReviewStatus.Approved,
                CreatedAt = DateTime.UtcNow
            });

            _context.Notifications.Add(new Notification
            {
                UserId = transaction.SellerId,
                Title = "New review on your property",
                Message = $"{model.Rating}/5 for '{transaction.PropertyListing.Title}'.",
                Type = NotificationType.SystemAnnouncement,
                RelatedEntityId = transaction.PropertyListingId,
                RelatedEntityType = "PropertyListing",
                ActionUrl = $"/Properties/Details/{transaction.PropertyListingId}",
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Unique index on TransactionId: a double submit.
                TempData["Error"] = "You already reviewed this transaction.";
                return RedirectToAction("MyTransactions", "Transactions");
            }

            TempData["Success"] = "Thank you, your review is published.";
            return RedirectToAction("Details", "Properties", new { id = transaction.PropertyListingId });
        }

        private async Task<(Transaction? transaction, IActionResult? error)> LoadEligibleAsync(int transactionId)
        {
            var userId = _userManager.GetUserId(User)!;

            var transaction = await _context.Transactions
                .Include(t => t.PropertyListing)
                .Include(t => t.Seller)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null) return (null, NotFound());
            if (transaction.BuyerId != userId) return (null, Forbid());

            var latestAgreement = await _context.RentalAgreements
                .AsNoTracking()
                .Where(a => a.TransactionId == transactionId)
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            if (!ReviewEligibility.IsEligible(transaction, latestAgreement, userId))
            {
                TempData["Error"] = ReviewEligibility.WhyNot(transaction);
                return (null, RedirectToAction("MyTransactions", "Transactions"));
            }

            if (await _context.PropertyReviews.AnyAsync(r => r.TransactionId == transactionId))
            {
                TempData["Error"] = "You already reviewed this transaction.";
                return (null, RedirectToAction("MyTransactions", "Transactions"));
            }

            return (transaction, null);
        }
    }
}
