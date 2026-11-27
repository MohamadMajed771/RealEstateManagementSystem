using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;
using PropertyTransferManagement.Services.Messaging;
using SharedContracts.Events;
using SharedContracts.Messaging;

namespace PropertyTransfer.Tests;

/// <summary>Emails are only counted, never sent.</summary>
public sealed class FakeEmail : IEmailSender
{
    public List<(string To, string Subject)> Sent { get; } = new();
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        Sent.Add((email, subject));
        return Task.CompletedTask;
    }
}

/// <summary>System 2 with an in-memory database and no RabbitMQ.</summary>
public sealed class TestHost
{
    private sealed class Consumer : TransferRequestsConsumer
    {
        public Consumer() : base(null!, null!, NullLogger<TransferRequestsConsumer>.Instance) { }
        public Task Handle(IServiceProvider services, ReceivedMessage message) =>
            HandleAsync(services, message, CancellationToken.None);
    }

    public FakeEmail Email { get; } = new();
    private readonly ServiceProvider _services;

    public TestHost()
    {
        var name = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(name));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddScoped<CaseService>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IEmailSender>(Email);
        _services = services.BuildServiceProvider();

        using var db = NewDb();
        foreach (var code in new[] { "BUYER_ID", "SELLER_ID", "OWNERSHIP_DEED", "SALE_AGREEMENT" })
            db.DocumentTypes.Add(new DocumentType { Code = code, Name = code, IsActive = true });
        db.SaveChanges();
    }

    public ApplicationDbContext NewDb()
    {
        var scope = _services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public IServiceScope NewScope() => _services.CreateScope();

    public async Task Send<T>(T evt, Guid? messageId = null) where T : TransferEvent
    {
        using var scope = _services.CreateScope();
        var message = new ReceivedMessage(messageId ?? Guid.NewGuid(), typeof(T).Name, "test",
            MessageSerializer.Serialize(evt), 0);
        await new Consumer().Handle(scope.ServiceProvider, message);
    }

    public static TransferRequested Request(Guid requestId) => new()
    {
        TransferRequestId = requestId,
        MarketplaceTransactionId = 7,
        MarketplacePropertyListingId = 9,
        PropertyTitle = "Test house",
        PropertyType = "House",
        Country = "Lebanon",
        City = "Batroun",
        SizeSquareMeters = 150,
        AgreedPrice = 170000,
        Buyer = new TransferParty { MarketplaceUserId = "b", FullName = "Bea Buyer", Email = "buyer@example.test" },
        Seller = new TransferParty { MarketplaceUserId = "s", FullName = "Sam Seller", Email = "seller@example.test" }
    };
}
