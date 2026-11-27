using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Services
{
    /// <summary>
    /// When a listing is approved, tells every user whose saved search matches it
    /// (spec §11): an in-app notification, plus an email when they asked for one.
    /// Matching uses the search page's own rules, so both always agree.
    /// </summary>
    public class SavedSearchNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly PropertySearchService _search;
        private readonly IEmailSender _email;
        private readonly ILogger<SavedSearchNotificationService> _logger;

        public SavedSearchNotificationService(ApplicationDbContext context, PropertySearchService search,
            IEmailSender email, ILogger<SavedSearchNotificationService> logger)
        {
            _context = context;
            _search = search;
            _email = email;
            _logger = logger;
        }

        /// <summary>The search-page filters a saved search stands for.</summary>
        public static PropertySearchCriteria ToCriteria(SavedSearch s) => new()
        {
            ListingType = s.ListingType,
            PropertyType = s.PropertyType,
            City = s.City ?? s.Area ?? s.Country,
            MinPrice = s.MinPrice,
            MaxPrice = s.MaxPrice,
            MinBedrooms = s.MinBedrooms,
            MinBathrooms = s.MinBathrooms,
            MinSize = s.MinSizeSquareMeters,
            MaxSize = s.MaxSizeSquareMeters,
            HasParking = s.HasParking,
            Furnished = s.Furnished,
            Lat = s.Latitude,
            Lng = s.Longitude,
            RadiusKm = s.RadiusKm
        };

        /// <param name="siteUrl">e.g. https://localhost:7287, used for the link in emails.</param>
        /// <returns>How many users were notified.</returns>
        public async Task<int> NotifyMatchingUsersAsync(int propertyListingId, string siteUrl)
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

            var searches = await _context.SavedSearches
                .AsNoTracking()
                .Include(s => s.User)
                .Where(s => s.InAppNotificationsEnabled && s.UserId != listing.Property.OwnerId)
                .ToListAsync();

            // shortcut: one small query per saved search keeps the rules identical to the
            // search page. Fine for hundreds of saved searches; batch it for many thousands.
            var matching = new List<SavedSearch>();
            foreach (var s in searches)
            {
                var criteria = ToCriteria(s);
                criteria.OnlyListingId = listing.Id;
                if ((await _search.SearchAsync(criteria)).Count > 0)
                    matching.Add(s);
            }

            // A user may have several matching searches: notify them once per listing,
            // and never twice for the same listing (e.g. after it is approved again).
            var alreadyNotified = await _context.Notifications
                .AsNoTracking()
                .Where(n =>
                    n.Type == NotificationType.SavedSearchMatch &&
                    n.RelatedEntityType == "PropertyListing" &&
                    n.RelatedEntityId == propertyListingId)
                .Select(n => n.UserId)
                .ToListAsync();

            var byUser = matching
                .Where(s => !alreadyNotified.Contains(s.UserId))
                .GroupBy(s => s.UserId)
                .ToList();

            var link = $"/Properties/Details/{listing.Id}";
            var summary = $"{listing.Title} in {listing.Property.City} is available for " +
                          $"{listing.Price:N2} {listing.Currency}.";

            foreach (var group in byUser)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = group.Key,
                    Title = "New property matches your saved search!",
                    Message = summary,
                    Type = NotificationType.SavedSearchMatch,
                    RelatedEntityId = listing.Id,
                    RelatedEntityType = "PropertyListing",
                    ActionUrl = link,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (byUser.Count > 0)
                await _context.SaveChangesAsync();

            // Emails go out after the notifications are saved; a failed email is only logged.
            foreach (var group in byUser)
            {
                var wantsEmail = group.FirstOrDefault(s => s.EmailNotificationsEnabled);
                var to = wantsEmail?.User?.Email;
                if (wantsEmail == null || string.IsNullOrEmpty(to)) continue;
                try
                {
                    var enc = HtmlEncoder.Default;
                    await _email.SendEmailAsync(to, "New property matches your saved search",
                        $"<p>Hello {enc.Encode(wantsEmail.User.FirstName)},</p>" +
                        $"<p>A new property matches your saved search <strong>{enc.Encode(wantsEmail.Name)}</strong>:</p>" +
                        $"<p>{enc.Encode(summary)}</p>" +
                        $"<p><a href=\"{enc.Encode(siteUrl.TrimEnd('/') + link)}\">View the property</a></p>" +
                        "<p>You can turn these emails off on your Saved Searches page.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Saved-search email could not be sent.");
                }
            }

            return byUser.Count;
        }
    }
}
