using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Models;
using SharedContracts.Events;

namespace PropertyTransfer.Tests;

/// <summary>System 2's handling of messages from the Marketplace (no RabbitMQ needed).</summary>
public class TransferRequestsTests
{
    private readonly TestHost _host = new();
    private readonly Guid _requestId = Guid.NewGuid();

    private async Task<TransferCase?> Case() =>
        await _host.NewDb().TransferCases.SingleOrDefaultAsync(c => c.MarketplaceTransferRequestId == _requestId);

    private async Task<List<string>> OutboxTypes() =>
        await _host.NewDb().OutboxMessages.OrderBy(m => m.CreatedAt).Select(m => m.EventType).ToListAsync();

    [Fact]
    public async Task Request_creates_one_case_with_a_document_checklist_and_confirms_it()
    {
        await _host.Send(TestHost.Request(_requestId));

        var c = await Case();
        Assert.NotNull(c);
        Assert.StartsWith("TR-", c!.CaseNumber);
        Assert.Equal(4, await _host.NewDb().CaseDocumentRequirements.CountAsync(r => r.TransferCaseId == c.Id));
        Assert.Contains(nameof(TransferCaseCreated), await OutboxTypes());
    }

    [Fact]
    public async Task Same_request_sent_twice_creates_only_one_case()
    {
        await _host.Send(TestHost.Request(_requestId));
        await _host.Send(TestHost.Request(_requestId)); // e.g. re-sent with a new message id

        Assert.Equal(1, await _host.NewDb().TransferCases.CountAsync());
    }

    [Fact]
    public async Task Cancel_that_overtakes_the_request_stops_the_case_being_opened()
    {
        await _host.Send(new TransferCancelled { TransferRequestId = _requestId, Reason = "Changed my mind." });
        await _host.Send(TestHost.Request(_requestId));

        Assert.Null(await Case());
    }

    [Fact]
    public async Task Cancel_of_a_new_case_cancels_it_and_tells_the_Marketplace()
    {
        await _host.Send(TestHost.Request(_requestId));
        await _host.Send(new TransferCancelled { TransferRequestId = _requestId, Reason = "Changed my mind." });

        Assert.Equal(TransferCaseStatus.Cancelled, (await Case())!.Status);
        Assert.Contains(nameof(TransferStatusChanged), await OutboxTypes());
    }

    [Fact]
    public async Task Cancel_during_official_processing_is_refused()
    {
        await _host.Send(TestHost.Request(_requestId));
        var db = _host.NewDb();
        var c = await db.TransferCases.SingleAsync();
        c.Status = TransferCaseStatus.ExternalProcessingInProgress;
        await db.SaveChangesAsync();

        await _host.Send(new TransferCancelled { TransferRequestId = _requestId, Reason = "Too late?" });

        Assert.Equal(TransferCaseStatus.ExternalProcessingInProgress, (await Case())!.Status);
        Assert.Contains(nameof(TransferCancellationRefused), await OutboxTypes());
    }

    [Fact]
    public async Task Name_change_in_the_Marketplace_updates_the_case_and_its_history()
    {
        await _host.Send(TestHost.Request(_requestId));
        await _host.Send(new TransferInformationUpdated { TransferRequestId = _requestId, PartyRole = "Buyer", FullName = "Bea New-Name" });

        var c = await Case();
        Assert.Equal("Bea New-Name", c!.BuyerFullName);
        Assert.Equal("Sam Seller", c.SellerFullName);
        Assert.True(await _host.NewDb().CaseAuditLogs.AnyAsync(a => a.TransferCaseId == c.Id && a.Action == "PartyDetailsUpdated"));
    }

    [Fact]
    public async Task Name_change_before_the_case_exists_is_retried_later()
    {
        var update = new TransferInformationUpdated { TransferRequestId = _requestId, PartyRole = "Seller", FullName = "Sam X" };

        // Throwing makes the consumer retry the message after the request.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _host.Send(update));
    }
}
