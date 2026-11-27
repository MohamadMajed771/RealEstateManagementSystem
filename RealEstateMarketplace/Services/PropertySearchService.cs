using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.ViewModels;

namespace RealEstateMarketplace.Services
{
    /// <summary>
    /// Search filters. Property names match the query-string names
    /// used by the search form, saved searches and the AI search.
    /// </summary>
    public class PropertySearchCriteria
    {
        public ListingType? ListingType { get; set; }
        public PropertyType? PropertyType { get; set; }
        public string? City { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? MinBedrooms { get; set; }
        public int? MinBathrooms { get; set; }
        public decimal? MinSize { get; set; }
        public decimal? MaxSize { get; set; }
        public bool? HasParking { get; set; }
        public bool? Furnished { get; set; }

        // Distance search around a point chosen on the map
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public double? RadiusKm { get; set; }

        // Soft preferences (AI search): only used for ranking when the
        // listing text actually mentions them. Never invents data.
        public List<string> Keywords { get; set; } = new();

        public bool HasDistanceFilter =>
            Lat is >= -90 and <= 90 && Lng is >= -180 and <= 180 && RadiusKm is > 0 and <= 500;
    }

    public class PropertySearchService
    {
        private readonly ApplicationDbContext _context;

        public PropertySearchService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PublicPropertyCardViewModel>> SearchAsync(PropertySearchCriteria c)
        {
            var now = DateTime.UtcNow;

            // Only approved, non-deleted listings are public.
            var query = _context.PropertyListings
                .AsNoTracking()
                .Where(l => l.Status == ListingStatus.Approved && !l.IsDeleted);

            if (c.ListingType.HasValue)
                query = query.Where(l => l.ListingType == c.ListingType.Value);

            if (c.PropertyType.HasValue)
                query = query.Where(l => l.Property.Type == c.PropertyType.Value);

            if (!string.IsNullOrWhiteSpace(c.City))
            {
                var place = c.City.Trim();
                query = query.Where(l =>
                    l.Property.City.Contains(place) || l.Property.Area.Contains(place) ||
                    l.Property.Country.Contains(place));
            }

            if (c.MinPrice is >= 0) query = query.Where(l => l.Price >= c.MinPrice.Value);
            if (c.MaxPrice is >= 0) query = query.Where(l => l.Price <= c.MaxPrice.Value);
            if (c.MinBedrooms is >= 0) query = query.Where(l => l.Property.Bedrooms >= c.MinBedrooms.Value);
            if (c.MinBathrooms is >= 0) query = query.Where(l => l.Property.Bathrooms >= c.MinBathrooms.Value);
            if (c.MinSize is >= 0) query = query.Where(l => l.Property.SizeSquareMeters >= c.MinSize.Value);
            if (c.MaxSize is > 0) query = query.Where(l => l.Property.SizeSquareMeters <= c.MaxSize.Value);
            if (c.HasParking == true) query = query.Where(l => l.Property.ParkingSpaces > 0);
            if (c.Furnished.HasValue) query = query.Where(l => l.Property.IsFurnished == c.Furnished.Value);

            // Bounding box in SQL, exact distance in memory.
            if (c.HasDistanceFilter)
            {
                var latDelta = (decimal)(c.RadiusKm!.Value / 111.0);
                var lngDelta = (decimal)(c.RadiusKm.Value / (111.0 * Math.Max(0.1, Math.Cos(c.Lat!.Value * Math.PI / 180))));
                var lat = (decimal)c.Lat.Value;
                var lng = (decimal)c.Lng!.Value;

                query = query.Where(l =>
                    l.Property.Latitude != null && l.Property.Longitude != null &&
                    l.Property.Latitude >= lat - latDelta && l.Property.Latitude <= lat + latDelta &&
                    l.Property.Longitude >= lng - lngDelta && l.Property.Longitude <= lng + lngDelta);
            }

            var results = await query
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
                    ParkingSpaces = l.Property.ParkingSpaces,
                    IsFurnished = l.Property.IsFurnished,
                    Latitude = l.Property.Latitude,
                    Longitude = l.Property.Longitude,
                    RentalPeriodMonths = l.RentalPeriodMonths,
                    IsFeatured = l.FeaturedUntil != null && l.FeaturedUntil > now,
                    SearchText = (l.Title + " " + l.Description + " " + l.Property.Area + " " +
                                  l.Property.City + " " + (l.Property.OtherFacilities ?? "")).ToLower(),
                    CreatedAt = l.CreatedAt,
                    CoverPhotoPath = _context.PropertyPhotos
                        .Where(p => p.PropertyListingId == l.Id)
                        .OrderByDescending(p => p.IsCoverPhoto)
                        .ThenBy(p => p.DisplayOrder)
                        .Select(p => p.ImagePath)
                        .FirstOrDefault()
                })
                .ToListAsync();

            if (c.HasDistanceFilter)
            {
                foreach (var r in results)
                    r.DistanceKm = DistanceKm(c.Lat!.Value, c.Lng!.Value, (double)r.Latitude!, (double)r.Longitude!);

                results = results.Where(r => r.DistanceKm <= c.RadiusKm).ToList();
            }

            var keywords = c.Keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            foreach (var r in results)
                r.MatchedKeywords = keywords.Where(k => r.SearchText.Contains(k)).ToList();

            // Featured first, then best keyword match, then nearest / newest.
            return results
                .OrderByDescending(r => r.IsFeatured)
                .ThenByDescending(r => r.MatchedKeywords.Count)
                .ThenBy(r => r.DistanceKm ?? 0)
                .ThenByDescending(r => r.CreatedAt)
                .ToList();
        }

        public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLng = (lng2 - lng1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
