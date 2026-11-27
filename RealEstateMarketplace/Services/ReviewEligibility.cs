using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Services
{
    /// <summary>
    /// A buyer or tenant may review only after a verified transaction:
    ///  - Sale: the ownership transfer was completed (System 2 confirmed it).
    ///  - Rent: both parties approved the rental agreement and the rental has started.
    /// Viewing a listing or chatting with the owner never makes a user eligible.
    /// </summary>
    public static class ReviewEligibility
    {
        public static bool IsEligible(Transaction transaction, RentalAgreement? latestAgreement, string userId)
        {
            if (transaction.BuyerId != userId) return false;

            if (transaction.TransactionType == ListingType.Sale)
                return transaction.Status == TransactionStatus.Completed;

            return latestAgreement != null &&
                   latestAgreement.FinalizedAt != null &&
                   latestAgreement.TenantApproved && latestAgreement.LandlordApproved &&
                   latestAgreement.StartDate.Date <= DateTime.UtcNow.Date &&
                   latestAgreement.Status is RentalAgreementStatus.Scheduled
                       or RentalAgreementStatus.Active
                       or RentalAgreementStatus.Expired
                       or RentalAgreementStatus.Completed;
        }

        public static string WhyNot(Transaction transaction) =>
            transaction.TransactionType == ListingType.Sale
                ? "You can review once the ownership transfer is officially completed."
                : "You can review once the rental agreement is approved by both sides and the rental has started.";
    }
}
