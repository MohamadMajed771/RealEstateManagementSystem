using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

namespace RealEstateMarketplace.Areas.Admin.Controllers
{
    /// <summary>Admins can hide abusive reviews or restore them.</summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserModerationService _moderation;

        public ReviewsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            UserModerationService moderation)
        {
            _context = context;
            _userManager = userManager;
            _moderation = moderation;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var reviews = await _context.PropertyReviews
                .AsNoTracking()
                .Include(r => r.Reviewer)
                .Include(r => r.ReviewedUser)
                .Include(r => r.Transaction).ThenInclude(t => t.PropertyListing)
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .Take(300)
                .ToListAsync();

            return View(reviews);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, ReviewStatus status)
        {
            var review = await _context.PropertyReviews.FindAsync(id);
            if (review == null) return NotFound();

            review.Status = status;
            review.UpdatedAt = DateTime.UtcNow;
            _moderation.Audit(_userManager.GetUserId(User), $"Review{status}", "PropertyReview", id.ToString(),
                $"Review #{id} set to {status}.");
            await _context.SaveChangesAsync();

            TempData["Success"] = status == ReviewStatus.Approved ? "Review is visible again." : "Review hidden.";
            return RedirectToAction(nameof(Index));
        }
    }
}
