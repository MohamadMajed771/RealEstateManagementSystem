
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
        public async Task<IActionResult> Index()
        {
            var listings = await _context.PropertyListings
                .AsNoTracking()
                .Where(l =>
                    l.Status == ListingStatus.Approved &&
                    !l.IsDeleted)
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

            return View(property);
        }

    }
}
