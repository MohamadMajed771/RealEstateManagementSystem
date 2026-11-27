using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using SharedContracts.Events;
using SharedContracts.Messaging;

namespace RealEstateMarketplace.Services.Messaging
{
    /// <summary>
    /// Receives case updates from the Transfer Service (System 2)
    /// and updates the marketplace transaction + notifies both parties.
    /// </summary>
    public class TransferUpdatesConsumer : RabbitMqConsumerBase
    {
        public TransferUpdatesConsumer(
            RabbitMqConnection rabbit,
            IServiceScopeFactory scopeFactory,
            ILogger<TransferUpdatesConsumer> logger)
            : base(rabbit, scopeFactory, logger)
        {
        }

        protected override string QueueName => MessagingTopology.MarketplaceQueue;

        protected override async Task HandleAsync(
            IServiceProvider services, ReceivedMessage message, CancellationToken ct)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();

            // Duplicate-message protection
            if (await db.InboxMessages.AnyAsync(m => m.MessageId == message.MessageId, ct))
                return;

            TransferEvent evt = message.EventType switch
            {
                nameof(TransferCaseCreated) => message.Read<TransferCaseCreated>(),
                nameof(TransferStatusChanged) => message.Read<TransferStatusChanged>(),
                nameof(AdditionalDocumentsRequired) => message.Read<AdditionalDocumentsRequired>(),
                nameof(TransferCompleted) => message.Read<TransferCompleted>(),
                nameof(TransferRejected) => message.Read<TransferRejected>(),
                _ => throw new InvalidOperationException($"Unknown event type '{message.EventType}'.")
            };

            var request = await db.TransferRequests
                .Include(r => r.Transaction)
                    .ThenInclude(t => t.PropertyListing)
                .FirstOrDefaultAsync(r => r.Id == evt.TransferRequestId, ct)
                ?? throw new InvalidOperationException(
                    $"Transfer request {evt.TransferRequestId} not found.");

            var transaction = request.Transaction;
            var now = DateTime.UtcNow;
            var title = transaction.PropertyListing.Title;

            // Ignore late updates after the case already finished.
            var finished = request.Status is TransferRequestStatus.Completed
                or TransferRequestStatus.Rejected or TransferRequestStatus.Cancelled;

            if (!finished)
            {
                switch (evt)
                {
                    case TransferCaseCreated e:
                        request.Status = TransferRequestStatus.Accepted;
                        request.ExternalTransferCaseId = e.CaseId;
                        request.CaseNumber = e.CaseNumber;
                        request.ExternalStatus = e.Status;
                        request.AcknowledgedAt = now;
                        transaction.Status = TransactionStatus.TransferInProgress;

                        NotifyBoth(db, transaction,
                            $"Transfer case {e.CaseNumber} created",
                            $"The Transfer Service opened case {e.CaseNumber} for '{title}'. " +
                            "Create an account there with the same email you use here " +
                            "to upload your documents and follow the case.");
                        break;

                    case TransferStatusChanged e when e.NewStatus == "Cancelled":
                        // The officer cancelled the case: the sale is back to "confirmed",
                        // so the buyer can request a new transfer later.
                        request.Status = TransferRequestStatus.Cancelled;
                        request.ExternalStatus = e.NewStatus;
                        transaction.Status = TransactionStatus.Confirmed;
                        transaction.UpdatedAt = now;

                        NotifyBoth(db, transaction,
                            $"Transfer case {e.CaseNumber} cancelled",
                            $"The ownership-transfer case for '{title}' was cancelled by the Transfer Service." +
                            (string.IsNullOrWhiteSpace(e.Note) ? "" : $" Reason: {e.Note}"));
                        break;

                    case TransferStatusChanged e:
                        request.Status = TransferRequestStatus.InProgress;
                        request.ExternalStatus = e.NewStatus;

                        NotifyBoth(db, transaction,
                            $"Transfer case {e.CaseNumber}: {TransferStatusText.Humanize(e.NewStatus)}",
                            $"Your ownership-transfer case for '{title}' is now " +
                            $"'{TransferStatusText.Humanize(e.NewStatus)}'." +
                            (string.IsNullOrWhiteSpace(e.Note) ? "" : $" Note: {e.Note}"));
                        break;

                    case AdditionalDocumentsRequired e:
                        var text =
                            $"The officer on case {e.CaseNumber} needs: " +
                            string.Join(", ", e.DocumentNames) + "." +
                            (string.IsNullOrWhiteSpace(e.Note) ? "" : $" Note: {e.Note}");

                        if (e.RequiredFromBuyer)
                            Notify(db, transaction.BuyerId, transaction.Id, "Documents required", text);
                        if (e.RequiredFromSeller)
                            Notify(db, transaction.SellerId, transaction.Id, "Documents required", text);
                        break;

                    case TransferCompleted e:
                        request.Status = TransferRequestStatus.Completed;
                        request.ExternalStatus = "OfficialCompletionConfirmed";
                        request.CompletedAt = e.CompletedAt;

                        transaction.Status = TransactionStatus.Completed;
                        transaction.CompletedAt = e.CompletedAt;
                        transaction.UpdatedAt = now;

                        transaction.PropertyListing.Status = ListingStatus.Sold;
                        transaction.PropertyListing.UpdatedAt = now;

                        NotifyBoth(db, transaction,
                            $"Ownership transfer completed ({e.CaseNumber})",
                            $"Official completion of the transfer of '{title}' was recorded " +
                            $"({e.AuthorityName}, ref. {e.OfficialReferenceNumber}).");
                        break;

                    case TransferRejected e:
                        request.Status = TransferRequestStatus.Rejected;
                        request.RejectionReason = e.Reason;
                        request.ExternalStatus = "Rejected";

                        transaction.Status = TransactionStatus.Rejected;
                        transaction.UpdatedAt = now;

                        NotifyBoth(db, transaction,
                            $"Transfer case {e.CaseNumber} rejected",
                            $"The ownership-transfer case for '{title}' cannot proceed. Reason: {e.Reason}");
                        break;
                }

                request.UpdatedAt = now;
                request.ExternalStatusUpdatedAt = now;
            }
            else if (evt is TransferStatusChanged closed)
            {
                // e.g. "Case Closed" after completion: show it, nothing else changes.
                request.ExternalStatus = closed.NewStatus;
                request.ExternalStatusUpdatedAt = now;
            }

            db.InboxMessages.Add(new InboxMessage
            {
                MessageId = message.MessageId,
                EventType = message.EventType,
                CorrelationId = evt.TransferRequestId,
                ReceivedAt = now,
                ProcessedAt = now,
                Status = InboxMessageStatus.Processed
            });

            await db.SaveChangesAsync(ct);
        }

        private static void NotifyBoth(
            ApplicationDbContext db, Transaction transaction, string title, string message)
        {
            Notify(db, transaction.BuyerId, transaction.Id, title, message);
            Notify(db, transaction.SellerId, transaction.Id, title, message);
        }

        private static void Notify(
            ApplicationDbContext db, string userId, int transactionId, string title, string message)
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title.Length > 150 ? title[..150] : title,
                Message = message.Length > 1000 ? message[..1000] : message,
                Type = NotificationType.TransferStatusChanged,
                RelatedEntityId = transactionId,
                RelatedEntityType = "Transaction",
                ActionUrl = "/Transactions/MyTransactions",
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
