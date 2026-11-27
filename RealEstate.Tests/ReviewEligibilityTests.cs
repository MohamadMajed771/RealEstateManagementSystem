using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

namespace RealEstate.Tests;

public class ReviewEligibilityTests
{
    private static Transaction Deal(ListingType type, TransactionStatus status) => new()
    {
        TransactionType = type, BuyerId = TestDb.Buyer, SellerId = TestDb.Seller, Status = status
    };

    private static RentalAgreement Agreement(DateTime start, RentalAgreementStatus status) => new()
    {
        StartDate = start, EndDate = start.AddMonths(3), TenantApproved = true, LandlordApproved = true,
        FinalizedAt = start.AddDays(-1), Status = status
    };

    [Fact]
    public void Buyer_can_review_a_sale_only_after_the_transfer_is_completed()
    {
        Assert.True(ReviewEligibility.IsEligible(Deal(ListingType.Sale, TransactionStatus.Completed), null, TestDb.Buyer));
        Assert.False(ReviewEligibility.IsEligible(Deal(ListingType.Sale, TransactionStatus.TransferInProgress), null, TestDb.Buyer));
    }

    [Fact]
    public void Seller_can_never_review_their_own_deal()
    {
        Assert.False(ReviewEligibility.IsEligible(Deal(ListingType.Sale, TransactionStatus.Completed), null, TestDb.Seller));
    }

    [Fact]
    public void Tenant_can_review_only_once_the_rental_has_started()
    {
        var deal = Deal(ListingType.Rent, TransactionStatus.Confirmed);
        var started = Agreement(DateTime.UtcNow.Date.AddDays(-1), RentalAgreementStatus.Active);
        var notYet = Agreement(DateTime.UtcNow.Date.AddDays(1), RentalAgreementStatus.Scheduled);

        Assert.True(ReviewEligibility.IsEligible(deal, started, TestDb.Buyer));
        Assert.False(ReviewEligibility.IsEligible(deal, notYet, TestDb.Buyer));
        Assert.False(ReviewEligibility.IsEligible(deal, null, TestDb.Buyer));
    }
}
