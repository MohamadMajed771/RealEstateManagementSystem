using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.ViewModels;

namespace RealEstateMarketplace.Controllers
{
    public class PropertiesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PropertiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Properties
        [HttpGet]
        public async Task<IActionResult> Index(
            ListingType? listingType,
            PropertyType? propertyType,
            string? city,
            decimal? minPrice,
            decimal? maxPrice,
            int? minBedrooms,
            int? minBathrooms,
            decimal? minSize)
        {
            // Only approved and non-deleted listings
            var query = _context.PropertyListings
                .AsNoTracking()
                .Where(l =>
                    l.Status == ListingStatus.Approved &&
                    !l.IsDeleted);

            // Filter by sale or rent
            if (listingType.HasValue &&
                Enum.IsDefined(typeof(ListingType), listingType.Value))
            {
                query = query.Where(l =>
                    l.ListingType == listingType.Value);
            }

            // Filter by property type
            if (propertyType.HasValue &&
                Enum.IsDefined(typeof(PropertyType), propertyType.Value))
            {
                query = query.Where(l =>
                    l.Property.Type == propertyType.Value);
            }

            // Filter by city
            if (!string.IsNullOrWhiteSpace(city))
            {
                string searchCity = city.Trim();

                query = query.Where(l =>
                    l.Property.City.Contains(searchCity));
            }

            // Filter by minimum price
            if (minPrice.HasValue && minPrice.Value >= 0)
            {
                query = query.Where(l =>
                    l.Price >= minPrice.Value);
            }

            // Filter by maximum price
            if (maxPrice.HasValue && maxPrice.Value >= 0)
            {
                query = query.Where(l =>
                    l.Price <= maxPrice.Value);
            }

            // Filter by minimum bedrooms
            if (minBedrooms.HasValue && minBedrooms.Value >= 0)
            {
                query = query.Where(l =>
                    l.Property.Bedrooms >= minBedrooms.Value);
            }

            // Filter by minimum bathrooms
            if (minBathrooms.HasValue && minBathrooms.Value >= 0)
            {
                query = query.Where(l =>
                    l.Property.Bathrooms >= minBathrooms.Value);
            }

            // Filter by minimum surface area
            if (minSize.HasValue && minSize.Value >= 0)
            {
                query = query.Where(l =>
                    l.Property.SizeSquareMeters >= minSize.Value);
            }

            // Save selected filters for the view
            ViewBag.ListingType = listingType;
            ViewBag.PropertyType = propertyType;
            ViewBag.City = city;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.MinBedrooms = minBedrooms;
            ViewBag.MinBathrooms = minBathrooms;
            ViewBag.MinSize = minSize;

            // Execute filtered query
            var listings = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new PublicPropertyCardViewModel
                {
                    Id = l.Id,
                    Title = l.Title,
                    PropertyType = l.Property.Type,
                    ListingType = l.ListingType,
                    Price = l.Price,
                    Currency = l.Currency,
                    City = l.Property.City,
                    Area = l.Property.Area,
                    SizeSquareMeters = l.Property.SizeSquareMeters,
                    Bedrooms = l.Property.Bedrooms,
                    Bathrooms = l.Property.Bathrooms,
                    CreatedAt = l.CreatedAt,

                    CoverPhotoPath = _context.PropertyPhotos
                        .Where(p => p.PropertyListingId == l.Id)
                        .OrderByDescending(p => p.IsCoverPhoto)
                        .ThenBy(p => p.DisplayOrder)
                        .Select(p => p.ImagePath)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return View(listings);
        }

        // GET: /Properties/Details/4
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var property = await _context.PropertyListings
                .AsNoTracking()
                .Where(l =>
                    l.Id == id &&
                    l.Status == ListingStatus.Approved &&
                    !l.IsDeleted)
                .Select(l => new PublicPropertyDetailsViewModel
                {
                    Id = l.Id,
                    Title = l.Title,
                    Description = l.Description,
                    ListingType = l.ListingType,
                    PropertyType = l.Property.Type,
                    Price = l.Price,
                    Currency = l.Currency,

                    Country = l.Property.Country,
                    City = l.Property.City,
                    Area = l.Property.Area,
                    StreetAddress = l.Property.StreetAddress,
                    Latitude = l.Property.Latitude,
                    Longitude = l.Property.Longitude,

                    SizeSquareMeters = l.Property.SizeSquareMeters,
                    Bedrooms = l.Property.Bedrooms,
                    Bathrooms = l.Property.Bathrooms,

                    SellerId = l.Property.OwnerId,

                    SellerName =
                        l.Property.Owner.FirstName + " " +
                        l.Property.Owner.LastName,

                    PublishedAt = l.PublishedAt,

                    Photos = _context.PropertyPhotos
                        .Where(p => p.PropertyListingId == l.Id)
                        .OrderByDescending(p => p.IsCoverPhoto)
                        .ThenBy(p => p.DisplayOrder)
                        .Select(p => new PublicPropertyPhotoViewModel
                        {
                            Id = p.Id,
                            ImagePath = p.ImagePath,
                            Caption = p.Caption,
                            IsCoverPhoto = p.IsCoverPhoto,
                            DisplayOrder = p.DisplayOrder
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (property == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ViewBag.IsFavorite = false;

            if (!string.IsNullOrEmpty(userId))
            {
                ViewBag.IsFavorite = await _context.Favorites
                    .AsNoTracking()
                    .AnyAsync(f =>
                        f.UserId == userId &&
                        f.PropertyListingId == id);
            }

            return View(property);
        }
    }
}
