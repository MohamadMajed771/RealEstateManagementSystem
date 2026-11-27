using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PropertyTransferManagement.Hubs;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Services
{
    /// <summary>
    /// After a successful save, pushes every new CaseNotification to its
    /// user over SignalR, wherever it was created (controllers, hubs or
    /// the RabbitMQ consumer).
    /// </summary>
    public class NotificationPushInterceptor : SaveChangesInterceptor
    {
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILogger<NotificationPushInterceptor> _logger;
        private readonly ConditionalWeakTable<DbContext, List<CaseNotification>> _pending = new();

        public NotificationPushInterceptor(IHubContext<NotificationHub> hub, ILogger<NotificationPushInterceptor> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Collect(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Collect(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            await PushAsync(eventData.Context);
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            _ = PushAsync(eventData.Context);
            return base.SavedChanges(eventData, result);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            if (eventData.Context != null) _pending.Remove(eventData.Context);
            base.SaveChangesFailed(eventData);
        }

        private void Collect(DbContext? context)
        {
            if (context == null) return;
            var added = context.ChangeTracker.Entries<CaseNotification>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();
            if (added.Count == 0) return;

            _pending.AddOrUpdate(context, added);
        }

        private async Task PushAsync(DbContext? context)
        {
            if (context == null || !_pending.TryGetValue(context, out var notifications)) return;
            _pending.Remove(context);

            foreach (var n in notifications)
            {
                try
                {
                    await _hub.Clients.User(n.UserId).SendAsync("notification", new
                    {
                        id = n.Id,
                        title = n.Title,
                        message = n.Message,
                        url = n.ActionUrl,
                        createdAt = n.CreatedAt
                    });
                }
                catch (Exception ex)
                {
                    // Live push is best effort; the notification is already stored.
                    _logger.LogDebug(ex, "Live notification push failed.");
                }
            }
        }
    }
}
