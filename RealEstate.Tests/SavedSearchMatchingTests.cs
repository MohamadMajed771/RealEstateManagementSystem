using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

namespace RealEstate.Tests;

public class SavedSearchMatchingTests
{
    private sealed class FakeEmail : IEmailSender
    {
        public List<string> To { get; } = new();
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            To.Add(email);
            return Task.CompletedTask;
        }
    }

    // Batroun, and a point about 40 km away (Beirut)
    private const double NearLat = 34.2553, NearLng = 35.6581, FarLat = 33.8938, FarLng = 35.5018;

    [Fact]
    public async Task Only_saved_searches_whose_map_area_contains_the_listing_are_notified_once()
    {
        var db = new ApplicationDbContext(TestDb.NewOptions());
        foreach (var id in new[] { "owner", "near-user", "far-user" })
            db.Users.Add(new ApplicationUser { Id = id, UserName = id, Email = id + "@example.test", FirstName = id, LastName = "Test" });
        var property = new Property
        {
            OwnerId = "owner", Type = PropertyType.House, Country = "Lebanon", City = "Batroun", Area = "Coast",
            SizeSquareMeters = 150, Latitude = 34.27m, Longitude = 35.66m // ~1.6 km from Batroun centre
        };
        var listing = new PropertyListing
        {
            Property = property, Title = "Sea view house", Description = "A house near the sea in Batroun.",
            ListingType = ListingType.Rent, Price = 900, Status = ListingStatus.Approved
        };
        db.PropertyListings.Add(listing);
        db.SavedSearches.AddRange(
            new SavedSearch { UserId = "near-user", Name = "Near Batroun", Latitude = NearLat, Longitude = NearLng, RadiusKm = 5, EmailNotificationsEnabled = true },
            new SavedSearch { UserId = "far-user", Name = "Near Beirut", Latitude = FarLat, Longitude = FarLng, RadiusKm = 5, EmailNotificationsEnabled = true });
        await db.SaveChangesAsync();

        var email = new FakeEmail();
        var service = new SavedSearchNotificationService(db, new PropertySearchService(db), email,
            NullLogger<SavedSearchNotificationService>.Instance);

        Assert.Equal(1, await service.NotifyMatchingUsersAsync(listing.Id, "https://localhost:7287"));
        Assert.Equal("near-user", (await db.Notifications.SingleAsync()).UserId);
        Assert.Equal(new[] { "near-user@example.test" }, email.To);

        // Approved again later (e.g. after an edit): nobody is told twice.
        Assert.Equal(0, await service.NotifyMatchingUsersAsync(listing.Id, "https://localhost:7287"));
    }
}
