using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

namespace RealEstate.Tests;

public class RentalStatusTests
{
    private static readonly DateTime Now = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    private static (ApplicationDbContext db, RentalAgreement agreement) Setup(
        RentalAgreementStatus status, DateTime start, DateTime end)
    {
        var db = new ApplicationDbContext(TestDb.NewOptions());
        var deal = TestDb.AddDeal(db, ListingType.Rent, TransactionStatus.Confirmed, ListingStatus.Rented);
        var agreement = new RentalAgreement
        {
            TransactionId = deal.Id, StartDate = start, EndDate = end, MonthlyRent = 500,
            TenantApproved = true, LandlordApproved = true, FinalizedAt = start.AddDays(-1), Status = status
        };
        db.RentalAgreements.Add(agreement);
        db.SaveChanges();
        return (db, agreement);
    }

    [Fact]
    public async Task Scheduled_rental_becomes_active_on_its_start_date()
    {
        var (db, agreement) = Setup(RentalAgreementStatus.Scheduled, Now.Date, Now.Date.AddMonths(3));

        await RentalStatusService.UpdateAsync(db, Now);

        Assert.Equal(RentalAgreementStatus.Active, agreement.Status);
        Assert.Equal(ListingStatus.Rented, (await db.PropertyListings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Rental_is_still_active_on_its_last_day()
    {
        var (db, agreement) = Setup(RentalAgreementStatus.Active, Now.Date.AddMonths(-3), Now.Date);

        await RentalStatusService.UpdateAsync(db, Now);

        Assert.Equal(RentalAgreementStatus.Active, agreement.Status);
    }

    [Fact]
    public async Task Ended_rental_completes_and_puts_the_listing_back_on_the_market()
    {
        var (db, agreement) = Setup(RentalAgreementStatus.Active, Now.Date.AddMonths(-3), Now.Date.AddDays(-1));

        await RentalStatusService.UpdateAsync(db, Now);

        Assert.Equal(RentalAgreementStatus.Completed, agreement.Status);
        Assert.Equal(TransactionStatus.Completed, (await db.Transactions.SingleAsync()).Status);
        Assert.Equal(ListingStatus.Approved, (await db.PropertyListings.SingleAsync()).Status);
        Assert.Equal(2, await db.Notifications.CountAsync()); // landlord and tenant
    }
}
