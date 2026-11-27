using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;
using RealEstateMarketplace.ViewModels;

namespace RealEstateMarketplace.Controllers
{
    public class PropertiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PropertySearchService _search;

        public PropertiesController(
            ApplicationDbContext context,
            PropertySearchService search)
        {
            _context = context;
            _search = search;
        }

        // GET: /Properties
        // Main page: interactive map + advanced filters.
        [HttpGet]
        public async Task<IActionResult> Index(PropertySearchCriteria criteria)
        {
            var listings = await _search.SearchAsync(criteria);

            // Save selected filters for the view
            ViewBag.Criteria = criteria;
            ViewBag.ListingType = criteria.ListingType;
            ViewBag.PropertyType = criteria.PropertyType;
            ViewBag.City = criteria.City;
            ViewBag.MinPrice = criteria.MinPrice;
            ViewBag.MaxPrice = criteria.MaxPrice;
            ViewBag.MinBedrooms = criteria.MinBedrooms;
            ViewBag.MinBathrooms = criteria.MinBathrooms;
            ViewBag.MinSize = criteria.MinSize;

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
                    // Sold / rented listings stay readable (with their reviews), but cannot be requested.
                    (l.Status == ListingStatus.Approved ||
                     l.Status == ListingStatus.Sold ||
                     l.Status == ListingStatus.Rented) &&
                    !l.IsDeleted)
                .Select(l => new PublicPropertyDetailsViewModel
                {
                    Id = l.Id,
                    Status = l.Status,
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
                    ParkingSpaces = l.Property.ParkingSpaces,
                    IsFurnished = l.Property.IsFurnished,
                    HasBalcony = l.Property.HasBalcony,
                    HasGarden = l.Property.HasGarden,
                    HasTerrace = l.Property.HasTerrace,
                    OtherFacilities = l.Property.OtherFacilities,
                    RentalPeriodMonths = l.RentalPeriodMonths,
                    InstallmentsAccepted = l.InstallmentsAccepted,
                    InstallmentMonths = l.InstallmentMonths,
                    IsFeatured = l.FeaturedUntil != null && l.FeaturedUntil > DateTime.UtcNow,
                    ViewCount = l.ViewCount,
                    SellerMemberSince = l.Property.Owner.CreatedAt,
                    SellerPublishedListings = _context.PropertyListings.Count(o =>
                        o.Property.OwnerId == l.Property.OwnerId &&
                        o.Status == ListingStatus.Approved && !o.IsDeleted),

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

            // Count views by visitors (not the owner) for "most viewed" stats.
            if (userId != property.SellerId)
            {
                await _context.PropertyListings
                    .Where(l => l.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.ViewCount, l => l.ViewCount + 1));
            }

            // Reviews come only from completed, verified transactions.
            property.Reviews = await _context.PropertyReviews
                .AsNoTracking()
                .Where(r =>
                    r.Transaction.PropertyListingId == id &&
                    r.Status == ReviewStatus.Approved &&
                    !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new PropertyReviewItem
                {
                    ReviewerName = r.Reviewer.FirstName + " " + r.Reviewer.LastName.Substring(0, 1) + ".",
                    Rating = r.Rating,
                    AccuracyRating = r.AccuracyRating,
                    ConditionRating = r.ConditionRating,
                    CommunicationRating = r.CommunicationRating,
                    IsRental = r.Transaction.TransactionType == ListingType.Rent,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

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
