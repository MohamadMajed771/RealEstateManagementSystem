using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Services
{
    /// <summary>
    /// Moves approved rentals along by date:
    ///  - Scheduled -> Active on the start date.
    ///  - Active -> Completed the day after the end date; the deal is closed and
    ///    the listing goes back on the market so it can be rented again.
    /// </summary>
    public class RentalStatusService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RentalStatusService> _logger;

        public RentalStatusService(IServiceScopeFactory scopeFactory, ILogger<RentalStatusService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await UpdateAsync(db, DateTime.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Rental status check failed; retrying later.");
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }

        public static async Task UpdateAsync(ApplicationDbContext db, DateTime now, CancellationToken ct = default)
        {
            var today = now.Date;
            var tomorrow = today.AddDays(1);

            // The end date is the last rented day.
            var due = await db.RentalAgreements
                .Include(a => a.Transaction).ThenInclude(t => t.PropertyListing)
                .Where(a => (a.Status == RentalAgreementStatus.Scheduled && a.StartDate < tomorrow) ||
                            (a.Status == RentalAgreementStatus.Active && a.EndDate < today))
                .ToListAsync(ct);

            foreach (var agreement in due)
            {
                if (agreement.EndDate >= today)
                {
                    agreement.Status = RentalAgreementStatus.Active; // started, not ended yet
                    continue;
                }

                agreement.Status = RentalAgreementStatus.Completed;
                agreement.CompletedAt = now;

                var transaction = agreement.Transaction;
                transaction.Status = TransactionStatus.Completed;
                transaction.CompletedAt = now;
                transaction.UpdatedAt = now;

                var listing = transaction.PropertyListing;
                if (listing.Status == ListingStatus.Rented && !listing.IsDeleted)
                {
                    listing.Status = ListingStatus.Approved;
                    listing.UpdatedAt = now;
                }

                foreach (var userId in new[] { transaction.SellerId, transaction.BuyerId })
                {
                    db.Notifications.Add(new Notification
                    {
                        UserId = userId,
                        Title = "Rental ended",
                        Message = userId == transaction.SellerId
                            ? $"The rental of '{listing.Title}' has ended. The listing is available again."
                            : $"Your rental of '{listing.Title}' has ended.",
                        Type = NotificationType.TransactionStatusChanged,
                        RelatedEntityId = agreement.Id,
                        RelatedEntityType = "RentalAgreement",
                        ActionUrl = $"/RentalAgreements/Details/{agreement.Id}",
                        CreatedAt = now
                    });
                }
            }

            if (due.Count > 0) await db.SaveChangesAsync(ct);
        }
    }
}
