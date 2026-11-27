using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using SharedContracts.Events;
using SharedContracts.Messaging;

namespace RealEstateMarketplace.Services.Messaging
{
    /// <summary>
    /// Outbox pattern: events are saved in OutboxMessages in the same
    /// database transaction as the business change, then this worker
    /// publishes them to RabbitMQ. If RabbitMQ is down, nothing is lost:
    /// the rows stay Pending and are retried.
    /// </summary>
    public static class Outbox
    {
        public static void Add(ApplicationDbContext db, TransferEvent message)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = message.MessageId,
                EventType = message.GetType().Name,
                RoutingKey = MessagingTopology.RoutingKeyFor(message),
                Payload = MessageSerializer.Serialize(message),
                CorrelationId = message.TransferRequestId,
                CreatedAt = DateTime.UtcNow,
                Status = OutboxMessageStatus.Pending
            });
        }
    }

    public class OutboxPublisherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitMqConnection _rabbit;
        private readonly ILogger<OutboxPublisherService> _logger;

        public OutboxPublisherService(
            IServiceScopeFactory scopeFactory,
            RabbitMqConnection rabbit,
            ILogger<OutboxPublisherService> logger)
        {
            _scopeFactory = scopeFactory;
            _rabbit = rabbit;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PublishPendingAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Outbox publishing failed: {Error}", ex.Message);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        private async Task PublishPendingAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;

            var pending = await db.OutboxMessages
                .Where(m =>
                    m.Status == OutboxMessageStatus.Pending &&
                    (m.NextAttemptAt == null || m.NextAttemptAt <= now))
                .OrderBy(m => m.CreatedAt)
                .Take(20)
                .ToListAsync(ct);

            foreach (var message in pending)
            {
                try
                {
                    _rabbit.Publish(
                        message.RoutingKey,
                        message.Id,
                        message.EventType,
                        message.CorrelationId,
                        message.Payload);

                    message.Status = OutboxMessageStatus.Published;
                    message.ProcessedAt = DateTime.UtcNow;
                    message.LastError = null;
                }
                catch (Exception ex)
                {
                    message.RetryCount++;
                    message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                    message.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(60, 5 * message.RetryCount));

                    _logger.LogWarning(
                        "Could not publish {EventType} {Id} (attempt {Attempt}): {Error}",
                        message.EventType, message.Id, message.RetryCount, ex.Message);

                    // Broker is probably down: stop here to keep message order.
                    await db.SaveChangesAsync(ct);
                    return;
                }

                await db.SaveChangesAsync(ct);
            }
        }
    }
}
