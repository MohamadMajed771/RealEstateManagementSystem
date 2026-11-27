using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services.Messaging;
using SharedContracts.Events;
using SharedContracts.Messaging;

namespace RealEstate.Tests;

/// <summary>System 1's handling of messages from the Transfer Service (no RabbitMQ needed).</summary>
public class TransferUpdatesTests
{
    private sealed class Consumer : TransferUpdatesConsumer
    {
        public Consumer() : base(null!, null!, NullLogger<TransferUpdatesConsumer>.Instance) { }
        public Task Handle(IServiceProvider services, ReceivedMessage message) =>
            HandleAsync(services, message, CancellationToken.None);
    }

    private readonly ServiceProvider _services;
    private readonly Guid _requestId = Guid.NewGuid();

    public TransferUpdatesTests()
    {
        var options = TestDb.NewOptions();
        _services = new ServiceCollection()
            .AddScoped(_ => new ApplicationDbContext(options))
            .BuildServiceProvider();

        using var db = new ApplicationDbContext(options);
        var deal = TestDb.AddDeal(db, ListingType.Sale, TransactionStatus.TransferInProgress);
        db.TransferRequests.Add(new TransferRequest
        {
            Id = _requestId, TransactionId = deal.Id,
            Status = TransferRequestStatus.CancellationRequested, ExternalTransferCaseId = Guid.NewGuid()
        });
        db.SaveChanges();
    }

    private async Task Send<T>(T evt, Guid? messageId = null) where T : TransferEvent
    {
        using var scope = _services.CreateScope();
        var message = new ReceivedMessage(messageId ?? Guid.NewGuid(), typeof(T).Name, "test",
            MessageSerializer.Serialize(evt), 0);
        await new Consumer().Handle(scope.ServiceProvider, message);
    }

    private async Task<(TransferRequest request, Transaction deal, int notifications)> State()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var request = await db.TransferRequests.Include(r => r.Transaction).SingleAsync();
        return (request, request.Transaction, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Progress_update_does_not_hide_a_pending_cancellation()
    {
        await Send(new TransferStatusChanged { TransferRequestId = _requestId, NewStatus = "UnderReview" });

        var (request, _, _) = await State();
        Assert.Equal(TransferRequestStatus.CancellationRequested, request.Status);
    }

    [Fact]
    public async Task Refused_cancellation_puts_the_transfer_back_in_progress()
    {
        await Send(new TransferCancellationRefused
        {
            TransferRequestId = _requestId, CurrentStatus = "ExternalProcessingInProgress", Reason = "Too late."
        });

        var (request, _, notifications) = await State();
        Assert.Equal(TransferRequestStatus.InProgress, request.Status);
        Assert.Equal(2, notifications); // buyer and seller
    }

    [Fact]
    public async Task Cancelled_case_frees_the_sale_for_a_new_request()
    {
        await Send(new TransferStatusChanged { TransferRequestId = _requestId, NewStatus = "Cancelled" });

        var (request, deal, _) = await State();
        Assert.Equal(TransferRequestStatus.Cancelled, request.Status);
        Assert.Equal(TransactionStatus.Confirmed, deal.Status);
    }

    [Fact]
    public async Task Same_message_twice_is_handled_once()
    {
        var id = Guid.NewGuid();
        var evt = new TransferCancellationRefused { TransferRequestId = _requestId, Reason = "Too late." };
        await Send(evt, id);
        await Send(evt, id);

        var (_, _, notifications) = await State();
        Assert.Equal(2, notifications);
    }
}
