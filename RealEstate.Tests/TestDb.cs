using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstate.Tests;

/// <summary>A fresh in-memory database per test, with one listing and one deal.</summary>
public static class TestDb
{
    public const string Buyer = "buyer-1";
    public const string Seller = "seller-1";

    public static DbContextOptions<ApplicationDbContext> NewOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    public static Transaction AddDeal(ApplicationDbContext db, ListingType type,
        TransactionStatus status, ListingStatus listingStatus = ListingStatus.Approved)
    {
        var listing = new PropertyListing
        {
            PropertyId = 1,
            Title = "Test house",
            Description = "A house used by the tests.",
            ListingType = type,
            Price = 1000,
            Status = listingStatus
        };
        var transaction = new Transaction
        {
            TransactionType = type,
            PropertyListing = listing,
            BuyerId = Buyer,
            SellerId = Seller,
            AgreedPrice = 1000,
            Status = status
        };
        db.Transactions.Add(transaction);
        db.SaveChanges();
        return transaction;
    }
}
