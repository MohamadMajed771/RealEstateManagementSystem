
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

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
            decimal? minSize,
            decimal? maxSize,
            bool? hasParking,
            bool? furnished,
            double? lat,
            double? lng,
            double? radiusKm,
            bool emailMe = false)
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
                MaxSizeSquareMeters = maxSize,
                HasParking = hasParking == true ? true : null,
                Furnished = furnished,
                EmailNotificationsEnabled = emailMe,
                CreatedAt = DateTime.UtcNow
            };

            // The map area is kept only when it is complete and valid.
            if (new PropertySearchCriteria { Lat = lat, Lng = lng, RadiusKm = radiusKm }.HasDistanceFilter)
            {
                savedSearch.Latitude = lat;
                savedSearch.Longitude = lng;
                savedSearch.RadiusKm = radiusKm;
            }

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

            var c = SavedSearchNotificationService.ToCriteria(savedSearch);
            return RedirectToAction(
                "Index",
                "Properties",
                new
                {
                    listingType = c.ListingType,
                    propertyType = c.PropertyType,
                    city = c.City,
                    minPrice = c.MinPrice,
                    maxPrice = c.MaxPrice,
                    minBedrooms = c.MinBedrooms,
                    minBathrooms = c.MinBathrooms,
                    minSize = c.MinSize,
                    maxSize = c.MaxSize,
                    hasParking = c.HasParking,
                    furnished = c.Furnished,
                    lat = c.Lat,
                    lng = c.Lng,
                    radiusKm = c.RadiusKm
                });
        }

        // POST: /SavedSearches/ToggleEmail/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEmail(int id)
        {
            var userId = _userManager.GetUserId(User);
            var savedSearch = await _context.SavedSearches
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
            if (savedSearch == null)
                return NotFound();

            savedSearch.EmailNotificationsEnabled = !savedSearch.EmailNotificationsEnabled;
            savedSearch.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = savedSearch.EmailNotificationsEnabled
                ? $"You will get an email when a new property matches \"{savedSearch.Name}\"."
                : $"Emails for \"{savedSearch.Name}\" are turned off.";
            return RedirectToAction(nameof(Index));
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
