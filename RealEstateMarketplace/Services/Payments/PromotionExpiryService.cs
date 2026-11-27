using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Services.Payments
{
    /// <summary>
    /// Marks promotions whose period ended as Expired.
    /// (The "featured" badge itself is driven by PropertyListing.FeaturedUntil,
    /// so it disappears on time even between runs.)
    /// </summary>
    public class PromotionExpiryService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PromotionExpiryService> _logger;

        public PromotionExpiryService(IServiceScopeFactory scopeFactory, ILogger<PromotionExpiryService> logger)
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
                    var now = DateTime.UtcNow;

                    int expired = await db.ListingPromotions
                        .Where(p => p.Status == ListingPromotionStatus.Active && p.EndDate != null && p.EndDate <= now)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.Status, ListingPromotionStatus.Expired)
                            .SetProperty(p => p.UpdatedAt, now), stoppingToken);

                    if (expired > 0) _logger.LogInformation("{Count} promotion(s) expired.", expired);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Promotion expiry check failed; retrying later.");
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}
