
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class SavedSearchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SavedSearchesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /SavedSearches
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var savedSearches = await _context.SavedSearches
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(savedSearches);
        }

        // POST: /SavedSearches/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string name,
            ListingType? listingType,
            PropertyType? propertyType,
            string? city,
            decimal? minPrice,
            decimal? maxPrice,
            int? minBedrooms,
            int? minBathrooms,
            decimal? minSize)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var savedSearch = new SavedSearch
            {
                UserId = userId,
                Name = name?.Trim() ?? string.Empty,
                ListingType = listingType,
                PropertyType = propertyType,
                City = string.IsNullOrWhiteSpace(city)
                    ? null
                    : city.Trim(),
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MinBedrooms = minBedrooms,
                MinBathrooms = minBathrooms,
                MinSizeSquareMeters = minSize,
                CreatedAt = DateTime.UtcNow
            };

            var validationResults = new List<
                System.ComponentModel.DataAnnotations.ValidationResult>();

            var validationContext =
                new System.ComponentModel.DataAnnotations.ValidationContext(
                    savedSearch);

            if (!System.ComponentModel.DataAnnotations.Validator
                .TryValidateObject(
                    savedSearch,
                    validationContext,
                    validationResults,
                    validateAllProperties: true))
            {
                TempData["ErrorMessage"] = string.Join(
                    " ",
                    validationResults.Select(r => r.ErrorMessage));

                return RedirectToAction("Index", "Properties");
            }

            _context.SavedSearches.Add(savedSearch);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Your search has been saved successfully.";

            return RedirectToAction(nameof(Index));
        }

        // GET: /SavedSearches/Run/5
        [HttpGet]
        public async Task<IActionResult> Run(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var savedSearch = await _context.SavedSearches
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.UserId == userId);

            if (savedSearch == null)
                return NotFound();

            return RedirectToAction(
                "Index",
                "Properties",
                new
                {
                    listingType = savedSearch.ListingType,
                    propertyType = savedSearch.PropertyType,
                    city = savedSearch.City,
                    minPrice = savedSearch.MinPrice,
                    maxPrice = savedSearch.MaxPrice,
                    minBedrooms = savedSearch.MinBedrooms,
                    minBathrooms = savedSearch.MinBathrooms,
                    minSize = savedSearch.MinSizeSquareMeters
                });
        }

        // POST: /SavedSearches/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var savedSearch = await _context.SavedSearches
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.UserId == userId);

            if (savedSearch == null)
                return NotFound();

            _context.SavedSearches.Remove(savedSearch);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Saved search deleted successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}
