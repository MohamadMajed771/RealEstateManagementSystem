using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using SharedContracts.Events;
using SharedContracts.Messaging;

namespace PropertyTransferManagement.Services.Messaging
{
    /// <summary>
    /// Receives TransferRequested / TransferCancelled from the Marketplace.
    /// Creates the transfer case, invitations for buyer and seller, the
    /// default document checklist, and publishes TransferCaseCreated.
    /// </summary>
    public class TransferRequestsConsumer : RabbitMqConsumerBase
    {
        public TransferRequestsConsumer(
            RabbitMqConnection rabbit,
            IServiceScopeFactory scopeFactory,
            ILogger<TransferRequestsConsumer> logger)
            : base(rabbit, scopeFactory, logger)
        {
        }

        protected override string QueueName => MessagingTopology.TransferServiceQueue;

        protected override async Task HandleAsync(
            IServiceProvider services, ReceivedMessage message, CancellationToken ct)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();

            // Duplicate-message protection
            if (await db.InboxMessages.AnyAsync(m => m.MessageId == message.MessageId, ct))
                return;

            TransferEvent evt = message.EventType switch
            {
                nameof(TransferRequested) => message.Read<TransferRequested>(),
                nameof(TransferCancelled) => message.Read<TransferCancelled>(),
                _ => throw new InvalidOperationException($"Unknown event type '{message.EventType}'.")
            };

            // Saved in the same SaveChanges as the business change.
            db.InboxMessages.Add(new InboxMessage
            {
                MessageId = message.MessageId,
                EventType = message.EventType,
                CorrelationId = evt.TransferRequestId,
                ReceivedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                Status = InboxMessageStatus.Processed
            });

            if (evt is TransferRequested requested)
                await CreateCaseAsync(services, db, requested, ct);
            else if (evt is TransferCancelled cancelled)
                await CancelCaseAsync(services, db, cancelled);

            await db.SaveChangesAsync(ct);
        }

        private static async Task CreateCaseAsync(
            IServiceProvider services, ApplicationDbContext db, TransferRequested request, CancellationToken ct)
        {
            // A second message for the same request (e.g. re-sent) is ignored.
            if (await db.TransferCases.AnyAsync(c => c.MarketplaceTransferRequestId == request.TransferRequestId, ct))
                return;

            var caseService = services.GetRequiredService<CaseService>();
            var config = services.GetRequiredService<IConfiguration>();
            var now = DateTime.UtcNow;

            var prefix = $"TR-{now.Year}-";
            var countThisYear = await db.TransferCases.CountAsync(c => c.CaseNumber.StartsWith(prefix), ct);

            var transferCase = new TransferCase
            {
                CaseNumber = prefix + (countThisYear + 1).ToString("D4"),
                MarketplaceTransferRequestId = request.TransferRequestId,
                MarketplaceTransactionId = request.MarketplaceTransactionId,
                MarketplacePropertyListingId = request.MarketplacePropertyListingId,
                PropertyTitle = Limit(request.PropertyTitle, 200),
                PropertyType = request.PropertyType,
                Country = Limit(request.Country, 100),
                City = Limit(request.City, 100),
                Area = request.Area == null ? null : Limit(request.Area, 100),
                StreetAddress = request.StreetAddress == null ? null : Limit(request.StreetAddress, 250),
                SizeSquareMeters = request.SizeSquareMeters,
                AgreedPrice = request.AgreedPrice,
                Currency = request.Currency,
                BuyerFullName = Limit(request.Buyer.FullName, 150),
                BuyerEmail = request.Buyer.Email,
                SellerFullName = Limit(request.Seller.FullName, 150),
                SellerEmail = request.Seller.Email,
                Status = TransferCaseStatus.RequestReceived,
                CreatedAt = now
            };

            db.TransferCases.Add(transferCase);

            db.CaseStatusHistories.Add(new CaseStatusHistory
            {
                TransferCaseId = transferCase.Id,
                PreviousStatus = TransferCaseStatus.RequestReceived,
                NewStatus = TransferCaseStatus.RequestReceived,
                Reason = "Transfer request received from the Marketplace.",
                ChangedAt = now
            });

            // Invitations: linked automatically when the person signs in
            // here with the same confirmed email address.
            foreach (var (email, role) in new[]
                     {
                         (request.Buyer.Email, CaseParticipantRole.Buyer),
                         (request.Seller.Email, CaseParticipantRole.Seller)
                     })
            {
                db.CaseInvitations.Add(new CaseInvitation
                {
                    TransferCaseId = transferCase.Id,
                    Email = email,
                    IntendedRole = role,
                    TokenHash = Convert.ToHexString(SHA256.HashData(RandomNumberGenerator.GetBytes(32))),
                    CreatedAt = now,
                    ExpiresAt = now.AddDays(60)
                });
            }

            // Default document checklist
            var documentTypes = await db.DocumentTypes.Where(d => d.IsActive).ToListAsync(ct);
            foreach (var type in documentTypes)
            {
                var role = DataSeeder.RequiredFromRole(type.Code);
                if (role == null) continue;

                db.CaseDocumentRequirements.Add(new CaseDocumentRequirement
                {
                    TransferCaseId = transferCase.Id,
                    DocumentTypeId = type.Id,
                    RequiredFromRole = role.Value,
                    IsMandatory = true,
                    Instructions = type.Description,
                    CreatedAt = now
                });
            }

            caseService.Audit(transferCase.Id, null, "CaseCreated", "TransferCase",
                transferCase.Id.ToString(),
                $"Case {transferCase.CaseNumber} created from marketplace transaction {request.MarketplaceTransactionId}.");

            // Optional automatic assignment to the least busy officer.
            if (config.GetValue<bool>("Transfer:AutoAssignOfficer"))
            {
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var officers = await userManager.GetUsersInRoleAsync(DataSeeder.OfficerRole);
                var officerIds = officers.Where(o => o.IsActive).Select(o => o.Id).ToList();

                if (officerIds.Count > 0)
                {
                    var load = await db.TransferCases
                        .Where(c => c.AssignedOfficerId != null && officerIds.Contains(c.AssignedOfficerId))
                        .GroupBy(c => c.AssignedOfficerId!)
                        .Select(g => new { g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

                    var officerId = officerIds.OrderBy(id => load.GetValueOrDefault(id)).First();

                    transferCase.AssignedOfficerId = officerId;
                    db.OfficerAssignments.Add(new OfficerAssignment
                    {
                        TransferCaseId = transferCase.Id,
                        OfficerId = officerId,
                        AssignedAt = now,
                        Notes = "Assigned automatically."
                    });

                    caseService.Notify(officerId, transferCase.Id,
                        $"New case {transferCase.CaseNumber} assigned to you",
                        $"{transferCase.PropertyTitle} — {transferCase.AgreedPrice:N0} {transferCase.Currency}.",
                        CaseNotificationType.OfficerAssigned);
                }
            }

            Outbox.Add(db, new TransferCaseCreated
            {
                TransferRequestId = request.TransferRequestId,
                CaseId = transferCase.Id,
                CaseNumber = transferCase.CaseNumber,
                Status = transferCase.Status.ToString()
            });

            await db.SaveChangesAsync(ct);

            // Best effort: email the parties (never fails the message).
            var emailSender = services.GetRequiredService<IEmailSender>();
            var baseUrl = config["Transfer:PublicBaseUrl"] ?? "https://localhost:7041";

            foreach (var email in new[] { request.Buyer.Email, request.Seller.Email })
            {
                try
                {
                    await emailSender.SendEmailAsync(email,
                        $"Ownership transfer case {transferCase.CaseNumber}",
                        $"<p>A transfer case was opened for <b>{System.Net.WebUtility.HtmlEncode(transferCase.PropertyTitle)}</b>.</p>" +
                        $"<p>Create an account at <a href='{baseUrl}'>{baseUrl}</a> with this email address " +
                        "to upload your documents and follow the case.</p>");
                }
                catch (Exception)
                {
                    // Email problems must not block case creation.
                }
            }
        }

        private static async Task CancelCaseAsync(
            IServiceProvider services, ApplicationDbContext db, TransferCancelled cancelled)
        {
            var transferCase = await db.TransferCases
                .FirstOrDefaultAsync(c => c.MarketplaceTransferRequestId == cancelled.TransferRequestId);

            if (transferCase == null || CaseWorkflow.IsFinished(transferCase.Status))
                return;

            var caseService = services.GetRequiredService<CaseService>();
            await caseService.ChangeStatusAsync(
                transferCase, TransferCaseStatus.Cancelled, null,
                $"Cancelled from the Marketplace. {cancelled.Reason}".Trim(),
                publishStatusChanged: false);
        }

        private static string Limit(string value, int max) =>
            value.Length > max ? value[..max] : value;
    }
}
