using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services.Payments;

namespace RealEstateMarketplace.Controllers
{
    public class CheckoutViewModel
    {
        public int PaymentId { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string? LastError { get; set; }

        [Required, Display(Name = "Name on card"), StringLength(100)]
        public string CardholderName { get; set; } = string.Empty;

        [Required, Display(Name = "Card number"), StringLength(23)]
        public string CardNumber { get; set; } = string.Empty;

        [Range(1, 12), Display(Name = "Month")]
        public int ExpMonth { get; set; }

        [Range(2000, 2100), Display(Name = "Year")]
        public int ExpYear { get; set; }

        [Required, StringLength(4, MinimumLength = 3), Display(Name = "CVC")]
        public string Cvc { get; set; } = string.Empty;
    }

    /// <summary>
    /// Owners pay (dummy gateway) to feature an approved listing.
    /// This pays for advertising only, never for the property itself.
    /// </summary>
    [Authorize]
    public class PromotionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DummyPaymentGateway _gateway;

        public PromotionsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            DummyPaymentGateway gateway)
        {
            _context = context;
            _userManager = userManager;
            _gateway = gateway;
        }

        // Choose a plan
        [HttpGet]
        public async Task<IActionResult> Promote(int listingId)
        {
            var listing = await LoadOwnedApprovedListingAsync(listingId);
            if (listing == null) return RedirectToAction("MyListings", "PropertyListings");

            ViewBag.Listing = listing;
            var plans = await _context.PromotionPlans.AsNoTracking()
                .Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync();
            return View(plans);
        }

        // Create the pending promotion + payment, then go to checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int listingId, int planId)
        {
            var userId = _userManager.GetUserId(User)!;
            var listing = await LoadOwnedApprovedListingAsync(listingId);
            if (listing == null) return RedirectToAction("MyListings", "PropertyListings");

            var plan = await _context.PromotionPlans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive);
            if (plan == null) return NotFound();

            // Reuse an unfinished checkout for the same listing + plan
            var pending = await _context.Payments
                .Include(p => p.ListingPromotion)
                .Where(p => p.UserId == userId &&
                            p.Status == PaymentStatus.Pending &&
                            p.ListingPromotion.PropertyListingId == listingId &&
                            p.ListingPromotion.PromotionPlanId == planId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (pending != null)
                return RedirectToAction(nameof(Checkout), new { id = pending.Id });

            var promotion = new ListingPromotion
            {
                PropertyListingId = listingId,
                PromotionPlanId = plan.Id,
                UserId = userId,
                AmountPaid = plan.Price,
                Currency = plan.Currency,
                DurationDays = plan.DurationDays,
                Status = ListingPromotionStatus.PendingPayment
            };

            var payment = new Payment
            {
                UserId = userId,
                ListingPromotion = promotion,
                Amount = plan.Price,
                Currency = plan.Currency,
                Method = PaymentMethod.TestPayment,
                Status = PaymentStatus.Pending,
                IdempotencyKey = Guid.NewGuid().ToString("N")
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Checkout), new { id = payment.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(int id)
        {
            var payment = await LoadOwnPaymentAsync(id);
            if (payment == null) return NotFound();
            if (payment.Status == PaymentStatus.Succeeded) return RedirectToAction(nameof(Receipt), new { id });

            return View(ToCheckout(payment, new CheckoutViewModel
            {
                ExpMonth = DateTime.UtcNow.Month,
                ExpYear = DateTime.UtcNow.Year + 2
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(int id, CheckoutViewModel model)
        {
            var payment = await LoadOwnPaymentAsync(id);
            if (payment == null) return NotFound();

            // Double submit / back button: never charge twice
            if (payment.Status == PaymentStatus.Succeeded)
                return RedirectToAction(nameof(Receipt), new { id });

            if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Failed)
                return RedirectToAction("MyListings", "PropertyListings");

            if (!ModelState.IsValid)
                return View(ToCheckout(payment, model));

            var listing = payment.ListingPromotion.PropertyListing;
            if (listing.Status != ListingStatus.Approved || listing.IsDeleted)
            {
                model.LastError = "Only published listings can be promoted.";
                return View(ToCheckout(payment, model));
            }

            payment.Status = PaymentStatus.Processing;
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var result = _gateway.Charge(payment.Amount, payment.Currency, model.CardNumber,
                model.ExpMonth, model.ExpYear, model.Cvc);

            var now = DateTime.UtcNow;
            var promotion = payment.ListingPromotion;

            if (!result.Succeeded)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = result.FailureReason;
                payment.UpdatedAt = now;
                await _context.SaveChangesAsync();

                model.LastError = result.FailureReason + " No money was taken. You can try again.";
                model.CardNumber = string.Empty;
                model.Cvc = string.Empty;
                return View(ToCheckout(payment, model));
            }

            payment.Status = PaymentStatus.Succeeded;
            payment.ProviderTransactionId = result.ProviderTransactionId;
            payment.PaidAt = now;
            payment.UpdatedAt = now;

            // A new promotion starts after any promotion still running
            var start = listing.FeaturedUntil != null && listing.FeaturedUntil > now ? listing.FeaturedUntil.Value : now;
            promotion.Status = ListingPromotionStatus.Active;
            promotion.StartDate = start;
            promotion.EndDate = start.AddDays(promotion.DurationDays);
            promotion.UpdatedAt = now;
            listing.FeaturedUntil = promotion.EndDate;
            listing.UpdatedAt = now;

            _context.Notifications.Add(new Notification
            {
                UserId = payment.UserId,
                Title = "Payment received — listing promoted",
                Message = $"'{listing.Title}' is featured until {promotion.EndDate:yyyy-MM-dd}. " +
                          $"Paid {payment.Amount:N2} {payment.Currency} ({result.ProviderTransactionId}).",
                Type = NotificationType.PaymentStatusChanged,
                RelatedEntityId = listing.Id,
                RelatedEntityType = "PropertyListing",
                ActionUrl = $"/Promotions/Receipt/{payment.Id}",
                CreatedAt = now
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Receipt), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Receipt(int id)
        {
            var payment = await LoadOwnPaymentAsync(id);
            if (payment == null || payment.Status != PaymentStatus.Succeeded) return NotFound();
            return View(payment);
        }

        [HttpGet]
        public async Task<IActionResult> MyPayments()
        {
            var userId = _userManager.GetUserId(User);
            var payments = await _context.Payments.AsNoTracking()
                .Include(p => p.ListingPromotion).ThenInclude(lp => lp.PropertyListing)
                .Include(p => p.ListingPromotion).ThenInclude(lp => lp.PromotionPlan)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return View(payments);
        }

        private async Task<PropertyListing?> LoadOwnedApprovedListingAsync(int listingId)
        {
            var userId = _userManager.GetUserId(User);
            var listing = await _context.PropertyListings
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l => l.Id == listingId && !l.IsDeleted && l.Property.OwnerId == userId);

            if (listing == null)
            {
                TempData["ErrorMessage"] = "Listing not found.";
                return null;
            }

            if (listing.Status != ListingStatus.Approved)
            {
                TempData["ErrorMessage"] = "Only listings approved by an administrator can be promoted.";
                return null;
            }

            return listing;
        }

        private async Task<Payment?> LoadOwnPaymentAsync(int id)
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Payments
                .Include(p => p.ListingPromotion).ThenInclude(lp => lp.PromotionPlan)
                .Include(p => p.ListingPromotion).ThenInclude(lp => lp.PropertyListing)
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        }

        private static CheckoutViewModel ToCheckout(Payment payment, CheckoutViewModel model)
        {
            model.PaymentId = payment.Id;
            model.ListingTitle = payment.ListingPromotion.PropertyListing.Title;
            model.PlanName = payment.ListingPromotion.PromotionPlan.Name;
            model.DurationDays = payment.ListingPromotion.DurationDays;
            model.Amount = payment.Amount;
            model.Currency = payment.Currency;
            return model;
        }
    }
}
