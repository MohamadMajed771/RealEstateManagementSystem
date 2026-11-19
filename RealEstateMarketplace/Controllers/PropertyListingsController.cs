
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;
using RealEstateMarketplace.ViewModels;

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class PropertyListingsController : Controller
    {
        private const int MaxPhotos = 10;
        private const long MaxPhotoSize = 5 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PropertyPhotoService _photoService;

        public PropertyListingsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            PropertyPhotoService photoService)
        {
            _context = context;
            _userManager = userManager;
            _photoService = photoService;
        }

        // ==========================================
        // GET: CREATE PROPERTY LISTING
        // ==========================================

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreatePropertyListingViewModel());
        }

        // ==========================================
        // POST: CREATE PROPERTY LISTING
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreatePropertyListingViewModel model)
        {
            var photos = model.Photos ?? new List<IFormFile>();

            if (photos.Count > MaxPhotos)
            {
                ModelState.AddModelError(
                    nameof(model.Photos),
                    "You can upload a maximum of 10 photos.");
            }

            ValidatePhotoSizes(photos, nameof(model.Photos));

            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

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

            var uploadedPaths = new List<string>();

            try
            {
                for (int i = 0; i < photos.Count; i++)
                {
                    var path = await _photoService
                        .UploadPhotoAsync(photos[i]);

                    uploadedPaths.Add(path);

                    _context.PropertyPhotos.Add(new PropertyPhoto
                    {
                        PropertyListing = listing,
                        ImagePath = path,
                        IsCoverPhoto = i == 0,
                        DisplayOrder = i,
                        UploadedAt = DateTime.UtcNow
                    });
                }

                _context.PropertyListings.Add(listing);

                await _context.SaveChangesAsync();
            }
            catch (ArgumentException ex)
            {
                await CleanupUploadedPhotosAsync(uploadedPaths);

                ModelState.AddModelError(
                    nameof(model.Photos),
                    ex.Message);

                return View(model);
            }
            catch
            {
                await CleanupUploadedPhotosAsync(uploadedPaths);
                throw;
            }

            TempData["SuccessMessage"] =
                "Your property listing and photos were submitted for approval.";

            return RedirectToAction(nameof(MyListings));
        }

        // ==========================================
        // GET: MY LISTINGS WITH PHOTOS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> MyListings()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var listings = await _context.PropertyListings
                .AsNoTracking()
                .Include(l => l.Property)
                .Where(l =>
                    l.Property.OwnerId == user.Id &&
                    !l.IsDeleted)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            var listingIds = listings
                .Select(l => l.Id)
                .ToList();

            var photos = await _context.PropertyPhotos
                .AsNoTracking()
                .Where(p => listingIds.Contains(p.PropertyListingId))
                .OrderByDescending(p => p.IsCoverPhoto)
                .ThenBy(p => p.DisplayOrder)
                .ThenBy(p => p.Id)
                .ToListAsync();

            ViewBag.ListingPhotos = photos
                .GroupBy(p => p.PropertyListingId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

            return View(listings);
        }

        // ==========================================
        // GET: EDIT PROPERTY LISTING
        // ==========================================

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
                Currency = listing.Currency,

                ExistingPhotos = await GetListingPhotosAsync(listing.Id)
            };

            model.CoverPhotoId = model.ExistingPhotos
                .FirstOrDefault(p => p.IsCoverPhoto)?.Id;

            return View(model);
        }

        // ==========================================
        // POST: EDIT PROPERTY LISTING AND PHOTOS
        // ==========================================

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

            var existingPhotos = await _context.PropertyPhotos
                .Where(p => p.PropertyListingId == listing.Id)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Id)
                .ToListAsync();

            var newPhotos = model.NewPhotos ?? new List<IFormFile>();

            var idsToDelete = (model.PhotoIdsToDelete ?? new List<int>())
                .Distinct()
                .ToHashSet();

            // Never allow deletion of another listing's photos.
            if (idsToDelete.Any(photoId =>
                !existingPhotos.Any(p => p.Id == photoId)))
            {
                ModelState.AddModelError(
                    nameof(model.PhotoIdsToDelete),
                    "Invalid photo selection.");
            }

            var remainingPhotos = existingPhotos
                .Where(p => !idsToDelete.Contains(p.Id))
                .ToList();

            if (remainingPhotos.Count + newPhotos.Count > MaxPhotos)
            {
                ModelState.AddModelError(
                    nameof(model.NewPhotos),
                    "A listing can contain a maximum of 10 photos.");
            }

            ValidatePhotoSizes(
                newPhotos,
                nameof(model.NewPhotos));

            // A cover photo must belong to the remaining photos.
            if (model.CoverPhotoId.HasValue &&
                !remainingPhotos.Any(p =>
                    p.Id == model.CoverPhotoId.Value))
            {
                ModelState.AddModelError(
                    nameof(model.CoverPhotoId),
                    "The selected cover photo is invalid.");
            }

            if (!ModelState.IsValid)
            {
                model.ExistingPhotos = existingPhotos;
                return View(model);
            }

            // Update property information.
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

            // Update advertisement information.
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

            var uploadedPaths = new List<string>();

            try
            {
                // Upload additional photos.
                int nextDisplayOrder = existingPhotos.Count == 0
                    ? 0
                    : existingPhotos.Max(p => p.DisplayOrder) + 1;

                foreach (var photo in newPhotos)
                {
                    var path = await _photoService
                        .UploadPhotoAsync(photo);

                    uploadedPaths.Add(path);

                    _context.PropertyPhotos.Add(new PropertyPhoto
                    {
                        PropertyListingId = listing.Id,
                        ImagePath = path,
                        IsCoverPhoto = false,
                        DisplayOrder = nextDisplayOrder++,
                        UploadedAt = DateTime.UtcNow
                    });
                }

                // Mark selected photos for deletion from SQL Server.
                var photosToDelete = existingPhotos
                    .Where(p => idsToDelete.Contains(p.Id))
                    .ToList();

                _context.PropertyPhotos.RemoveRange(photosToDelete);

                // Choose the cover photo.
                // If no existing cover remains, the first new photo
                // becomes the cover automatically.
                int? selectedCoverId = model.CoverPhotoId;

                if (!selectedCoverId.HasValue)
                {
                    selectedCoverId = remainingPhotos
                        .FirstOrDefault(p => p.IsCoverPhoto)?.Id
                        ?? remainingPhotos.FirstOrDefault()?.Id;
                }

                foreach (var photo in remainingPhotos)
                {
                    photo.IsCoverPhoto =
                        selectedCoverId.HasValue &&
                        photo.Id == selectedCoverId.Value;
                }

                // If there are no remaining existing photos,
                // assign cover status to the first new photo.
                if (!selectedCoverId.HasValue && newPhotos.Count > 0)
                {
                    var addedPhotos = _context.ChangeTracker
                        .Entries<PropertyPhoto>()
                        .Where(e =>
                            e.State == EntityState.Added &&
                            e.Entity.PropertyListingId == listing.Id)
                        .Select(e => e.Entity)
                        .OrderBy(p => p.DisplayOrder)
                        .ToList();

                    if (addedPhotos.Count > 0)
                    {
                        addedPhotos[0].IsCoverPhoto = true;
                    }
                }

                await _context.SaveChangesAsync();

                // Delete physical files only after successful DB save.
                foreach (var photo in photosToDelete)
                {
                    await _photoService.DeletePhotoAsync(photo.ImagePath);
                }
            }
            catch (ArgumentException ex)
            {
                await CleanupUploadedPhotosAsync(uploadedPaths);

                model.ExistingPhotos = existingPhotos;

                ModelState.AddModelError(
                    nameof(model.NewPhotos),
                    ex.Message);

                return View(model);
            }
            catch
            {
                await CleanupUploadedPhotosAsync(uploadedPaths);
                throw;
            }

            TempData["SuccessMessage"] =
                "Your listing and photos were updated and submitted for approval.";

            return RedirectToAction(nameof(MyListings));
        }

        // ==========================================
        // POST: SOFT DELETE PROPERTY LISTING
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
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

            listing.IsDeleted = true;
            listing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Your property listing was deleted successfully.";

            return RedirectToAction(nameof(MyListings));
        }

        // ==========================================
        // PHOTO HELPERS
        // ==========================================

        private async Task<List<PropertyPhoto>> GetListingPhotosAsync(
            int listingId)
        {
            return await _context.PropertyPhotos
                .AsNoTracking()
                .Where(p => p.PropertyListingId == listingId)
                .OrderByDescending(p => p.IsCoverPhoto)
                .ThenBy(p => p.DisplayOrder)
                .ThenBy(p => p.Id)
                .ToListAsync();
        }

        private void ValidatePhotoSizes(
            IEnumerable<IFormFile> photos,
            string modelStateKey)
        {
            foreach (var photo in photos)
            {
                if (photo.Length == 0 ||
                    photo.Length > MaxPhotoSize)
                {
                    ModelState.AddModelError(
                        modelStateKey,
                        "Each photo must be non-empty and 5 MB or smaller.");

                    break;
                }
            }
        }

        private async Task CleanupUploadedPhotosAsync(
            IEnumerable<string> imagePaths)
        {
            foreach (var path in imagePaths)
            {
                await _photoService.DeletePhotoAsync(path);
            }
        }
    }
}
