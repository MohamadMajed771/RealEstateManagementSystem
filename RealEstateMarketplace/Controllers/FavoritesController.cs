
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.ViewModels;

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FavoritesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Favorites
        // Display the current user's favorite properties.
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var favorites = await _context.Favorites
                .AsNoTracking()
                .Where(f =>
                    f.UserId == userId &&
                    f.PropertyListing.Status == ListingStatus.Approved &&
                    !f.PropertyListing.IsDeleted)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new PublicPropertyCardViewModel
                {
                    Id = f.PropertyListing.Id,
                    Title = f.PropertyListing.Title,
                    PropertyType = f.PropertyListing.Property.Type,
                    ListingType = f.PropertyListing.ListingType,
                    Price = f.PropertyListing.Price,
                    Currency = f.PropertyListing.Currency,
                    City = f.PropertyListing.Property.City,
                    Area = f.PropertyListing.Property.Area,
                    SizeSquareMeters =
                        f.PropertyListing.Property.SizeSquareMeters,
                    Bedrooms = f.PropertyListing.Property.Bedrooms,
                    Bathrooms = f.PropertyListing.Property.Bathrooms,
                    CreatedAt = f.PropertyListing.CreatedAt,

                    CoverPhotoPath = _context.PropertyPhotos
                        .Where(p =>
                            p.PropertyListingId == f.PropertyListingId)
                        .OrderByDescending(p => p.IsCoverPhoto)
                        .ThenBy(p => p.DisplayOrder)
                        .Select(p => p.ImagePath)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return View(favorites);
        }

        // POST: /Favorites/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(
            int propertyListingId,
            string? returnUrl)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            // Only allow saving publicly available listings.
            var listingExists = await _context.PropertyListings
                .AnyAsync(l =>
                    l.Id == propertyListingId &&
                    l.Status == ListingStatus.Approved &&
                    !l.IsDeleted);

            if (!listingExists)
            {
                return NotFound();
            }

            // Do not create duplicate favorites.
            var alreadySaved = await _context.Favorites
                .AnyAsync(f =>
                    f.UserId == userId &&
                    f.PropertyListingId == propertyListingId);

            if (!alreadySaved)
            {
                _context.Favorites.Add(new Favorite
                {
                    UserId = userId,
                    PropertyListingId = propertyListingId,
                    CreatedAt = DateTime.UtcNow
                });

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Handle a possible duplicate insert from
                    // two simultaneous requests.
                    var existsAfterFailure = await _context.Favorites
                        .AsNoTracking()
                        .AnyAsync(f =>
                            f.UserId == userId &&
                            f.PropertyListingId == propertyListingId);

                    if (!existsAfterFailure)
                    {
                        throw;
                    }
                }
            }

            TempData["SuccessMessage"] =
                "Property added to your favorites.";

            return RedirectBack(returnUrl);
        }

        // POST: /Favorites/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(
            int propertyListingId,
            string? returnUrl)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f =>
                    f.UserId == userId &&
                    f.PropertyListingId == propertyListingId);

            if (favorite != null)
            {
                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] =
                "Property removed from your favorites.";

            return RedirectBack(returnUrl);
        }

        // Prevent external redirects.
        private IActionResult RedirectBack(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
