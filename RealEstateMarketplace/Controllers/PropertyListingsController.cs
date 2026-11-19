
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
    public class PropertyListingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PropertyListingsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /PropertyListings/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreatePropertyListingViewModel());
        }

        // POST: /PropertyListings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreatePropertyListingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var property = new Property
            {
                OwnerId = user.Id,
                Type = model.Type!.Value,
                Country = model.Country.Trim(),
                City = model.City.Trim(),
                Area = model.Area.Trim(),
                StreetAddress = model.StreetAddress?.Trim(),
                SizeSquareMeters = model.SizeSquareMeters!.Value,
                Bedrooms = model.Bedrooms,
                Bathrooms = model.Bathrooms,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                CreatedAt = DateTime.UtcNow
            };

            var listing = new PropertyListing
            {
                Property = property,
                Title = model.Title.Trim(),
                Description = model.Description.Trim(),
                ListingType = model.ListingType!.Value,
                Price = model.Price!.Value,
                Currency = model.Currency.Trim().ToUpperInvariant(),
                Status = ListingStatus.PendingApproval,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            // Save both related records in one operation.
            _context.PropertyListings.Add(listing);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Your property listing was submitted for approval.";

            return RedirectToAction(nameof(MyListings));
        }

        // GET: /PropertyListings/MyListings
        [HttpGet]
        public async Task<IActionResult> MyListings()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var listings = await _context.PropertyListings
                .AsNoTracking()
                .Include(l => l.Property)
                .Where(l =>
                    l.Property.OwnerId == user.Id &&
                    !l.IsDeleted)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            return View(listings);
        }


        // GET: /PropertyListings/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var listing = await _context.PropertyListings
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    l.Property.OwnerId == user.Id &&
                    !l.IsDeleted);

            if (listing == null)
                return NotFound();

            var model = new EditPropertyListingViewModel
            {
                Id = listing.Id,
                Type = listing.Property.Type,
                Country = listing.Property.Country,
                City = listing.Property.City,
                Area = listing.Property.Area,
                StreetAddress = listing.Property.StreetAddress,
                SizeSquareMeters = listing.Property.SizeSquareMeters,
                Bedrooms = listing.Property.Bedrooms,
                Bathrooms = listing.Property.Bathrooms,
                Latitude = listing.Property.Latitude,
                Longitude = listing.Property.Longitude,
                Title = listing.Title,
                Description = listing.Description,
                ListingType = listing.ListingType,
                Price = listing.Price,
                Currency = listing.Currency
            };

            return View(model);
        }

        // POST: /PropertyListings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            EditPropertyListingViewModel model)
        {
            if (id != model.Id)
                return BadRequest();

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var listing = await _context.PropertyListings
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    l.Property.OwnerId == user.Id &&
                    !l.IsDeleted);

            if (listing == null)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            // Update property details
            listing.Property.Type = model.Type!.Value;
            listing.Property.Country = model.Country.Trim();
            listing.Property.City = model.City.Trim();
            listing.Property.Area = model.Area.Trim();
            listing.Property.StreetAddress =
                model.StreetAddress?.Trim();
            listing.Property.SizeSquareMeters =
                model.SizeSquareMeters!.Value;
            listing.Property.Bedrooms = model.Bedrooms;
            listing.Property.Bathrooms = model.Bathrooms;
            listing.Property.Latitude = model.Latitude;
            listing.Property.Longitude = model.Longitude;

            // Update advertisement details
            listing.Title = model.Title.Trim();
            listing.Description = model.Description.Trim();
            listing.ListingType = model.ListingType!.Value;
            listing.Price = model.Price!.Value;
            listing.Currency =
                model.Currency.Trim().ToUpperInvariant();

            // Editing requires moderation again.
            listing.Status = ListingStatus.PendingApproval;
            listing.UpdatedAt = DateTime.UtcNow;
            listing.PublishedAt = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Your listing was updated and submitted for approval.";

            return RedirectToAction(nameof(MyListings));
        }


        // POST: /PropertyListings/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            // Only the owner can delete their listing.
            var listing = await _context.PropertyListings
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l =>
                    l.Id == id &&
                    l.Property.OwnerId == user.Id &&
                    !l.IsDeleted);

            if (listing == null)
            {
                return NotFound();
            }

            // Soft delete: keep the database record.
            listing.IsDeleted = true;
            listing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Your property listing was deleted successfully.";

            return RedirectToAction(nameof(MyListings));
        }


    }
}
