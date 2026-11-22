
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Services
{
    public class SavedSearchNotificationService
    {
        private readonly ApplicationDbContext _context;

        public SavedSearchNotificationService(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> NotifyMatchingUsersAsync(
            int propertyListingId)
        {
            // Only approved, publicly visible listings qualify.
            var listing = await _context.PropertyListings
                .AsNoTracking()
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l =>
                    l.Id == propertyListingId &&
                    l.Status == ListingStatus.Approved &&
                    !l.IsDeleted);

            if (listing == null)
                return 0;

            var property = listing.Property;

            // Find enabled saved searches that match the listing.
            var matchingSearches = await _context.SavedSearches
                .AsNoTracking()
                .Where(s =>
                    s.InAppNotificationsEnabled &&

                    s.UserId != property.OwnerId &&

                    (!s.ListingType.HasValue ||
                     s.ListingType.Value == listing.ListingType) &&

                    (!s.PropertyType.HasValue ||
                     s.PropertyType.Value == property.Type) &&

                    (string.IsNullOrEmpty(s.Country) ||
                     s.Country.ToLower() ==
                         property.Country.ToLower()) &&

                    (string.IsNullOrEmpty(s.City) ||
                     property.City.ToLower().Contains(
                         s.City.ToLower())) &&

                    (string.IsNullOrEmpty(s.Area) ||
                     property.Area.ToLower().Contains(
                         s.Area.ToLower())) &&

                    (!s.MinPrice.HasValue ||
                     listing.Price >= s.MinPrice.Value) &&

                    (!s.MaxPrice.HasValue ||
                     listing.Price <= s.MaxPrice.Value) &&

                    (!s.MinSizeSquareMeters.HasValue ||
                     property.SizeSquareMeters >=
                         s.MinSizeSquareMeters.Value) &&

                    (!s.MaxSizeSquareMeters.HasValue ||
                     property.SizeSquareMeters <=
                         s.MaxSizeSquareMeters.Value) &&

                    (!s.MinBedrooms.HasValue ||
                     (property.Bedrooms.HasValue &&
                      property.Bedrooms.Value >=
                          s.MinBedrooms.Value)) &&

                    (!s.MinBathrooms.HasValue ||
                     (property.Bathrooms.HasValue &&
                      property.Bathrooms.Value >=
                          s.MinBathrooms.Value)))
                .ToListAsync();

            // A user may have multiple matching saved searches.
            // Notify each user only once for the same property.
            var matchingUserIds = matchingSearches
                .Select(s => s.UserId)
                .Distinct()
                .ToList();

            if (matchingUserIds.Count == 0)
                return 0;

            // Prevent duplicate notifications.
            var alreadyNotifiedUserIds = await _context
                .Set<Notification>()
                .AsNoTracking()
                .Where(n =>
                    matchingUserIds.Contains(n.UserId) &&
                    n.Type == NotificationType.SavedSearchMatch &&
                    n.RelatedEntityType == "PropertyListing" &&
                    n.RelatedEntityId == propertyListingId)
                .Select(n => n.UserId)
                .Distinct()
                .ToListAsync();

            var usersToNotify = matchingUserIds
                .Except(alreadyNotifiedUserIds)
                .ToList();

            foreach (var userId in usersToNotify)
            {
                _context.Set<Notification>().Add(
                    new Notification
                    {
                        UserId = userId,

                        Title = "New property matches your saved search!",

                        Message =
                            $"{listing.Title} in {property.City} " +
                            $"is available for {listing.Price:N2} " +
                            $"{listing.Currency}.",

                        Type = NotificationType.SavedSearchMatch,

                        RelatedEntityId = listing.Id,
                        RelatedEntityType = "PropertyListing",

                        ActionUrl = $"/Properties/Details/{listing.Id}",

                        IsRead = false,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    });
            }

            if (usersToNotify.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return usersToNotify.Count;
        }
    }
}
