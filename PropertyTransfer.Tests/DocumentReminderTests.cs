using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;

namespace PropertyTransfer.Tests;

public class DocumentReminderTests
{
    private readonly TestHost _host = new();

    private List<(string To, string Subject)> Reminders() =>
        _host.Email.Sent.Where(m => m.Subject.Contains("documents still missing")).ToList();

    private async Task<int> Run(DateTime now)
    {
        using var scope = _host.NewScope();
        var sp = scope.ServiceProvider;
        return await DocumentReminderService.RunOnceAsync(sp.GetRequiredService<ApplicationDbContext>(),
            sp.GetRequiredService<CaseService>(), _host.Email, "https://localhost:7041",
            NullLogger.Instance, now);
    }

    private async Task<DateTime> NewCaseRequested(TimeSpan ago)
    {
        await _host.Send(TestHost.Request(Guid.NewGuid()));
        var db = _host.NewDb();
        var when = DateTime.UtcNow - ago;
        foreach (var r in await db.CaseDocumentRequirements.ToListAsync()) r.CreatedAt = when;
        await db.SaveChangesAsync();
        return DateTime.UtcNow;
    }

    [Fact]
    public async Task No_reminder_while_the_documents_were_asked_for_recently()
    {
        var now = await NewCaseRequested(TimeSpan.FromDays(1));
        Assert.Equal(0, await Run(now));
    }

    [Fact]
    public async Task Missing_documents_are_reminded_once_per_party_then_not_again_for_three_days()
    {
        var now = await NewCaseRequested(TimeSpan.FromDays(3));

        Assert.Equal(2, await Run(now));                    // buyer and seller each get one email
        Assert.Equal(2, Reminders().Count);
        Assert.Equal(0, await Run(now.AddHours(1)));         // not again right away
        Assert.Equal(2, await Run(now.AddDays(3).AddMinutes(1))); // but again after three days
    }

    [Fact]
    public async Task Uploaded_documents_are_not_reminded()
    {
        var now = await NewCaseRequested(TimeSpan.FromDays(3));
        var db = _host.NewDb();
        foreach (var r in await db.CaseDocumentRequirements.Include(r => r.DocumentType).ToListAsync())
            if (r.RequiredFromRole == CaseParticipantRole.Seller) r.Status = DocumentRequirementStatus.Submitted;
        await db.SaveChangesAsync();

        Assert.Equal(1, await Run(now)); // only the buyer still owes documents
        Assert.Equal("buyer@example.test", Reminders().Single().To);
    }
}
