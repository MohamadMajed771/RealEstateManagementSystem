using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Services
{
    /// <summary>
    /// Automated reminders for missing documents (spec §40). When a mandatory
    /// document is still missing (or was rejected) two days after it was requested,
    /// the buyer or seller gets an in-app notification and an email, then again
    /// every three days until it is uploaded. Each reminder is written to the case
    /// history, which is also how repeats are avoided.
    /// </summary>
    public class DocumentReminderService : BackgroundService
    {
        public static readonly TimeSpan FirstReminderAfter = TimeSpan.FromDays(2);
        public static readonly TimeSpan RepeatEvery = TimeSpan.FromDays(3);
        public const string AuditAction = "DocumentReminderSent";

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<DocumentReminderService> _logger;

        public DocumentReminderService(IServiceScopeFactory scopes, ILogger<DocumentReminderService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var sp = scope.ServiceProvider;
                    await RunOnceAsync(sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<CaseService>(),
                        sp.GetRequiredService<IEmailSender>(), sp.GetRequiredService<IConfiguration>()["Transfer:PublicBaseUrl"],
                        _logger, DateTime.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Document reminder run failed; retrying later.");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        /// <returns>How many reminders were sent (one per party per case).</returns>
        public static async Task<int> RunOnceAsync(ApplicationDbContext db, CaseService cases, IEmailSender email,
            string? publicBaseUrl, ILogger logger, DateTime now, CancellationToken ct = default)
        {
            var requestedBefore = now - FirstReminderAfter;
            var missing = await db.CaseDocumentRequirements
                .Include(r => r.TransferCase)
                .Include(r => r.DocumentType)
                .Where(r => r.IsMandatory &&
                            (r.Status == DocumentRequirementStatus.Pending || r.Status == DocumentRequirementStatus.Rejected) &&
                            r.CreatedAt <= requestedBefore &&
                            r.TransferCase.Status != TransferCaseStatus.OnHold &&
                            r.TransferCase.Status != TransferCaseStatus.Cancelled &&
                            r.TransferCase.Status != TransferCaseStatus.Rejected &&
                            r.TransferCase.Status != TransferCaseStatus.OfficialCompletionConfirmed &&
                            r.TransferCase.Status != TransferCaseStatus.CaseClosed)
                .ToListAsync(ct);

            var recentAfter = now - RepeatEvery;
            var emails = new List<(string To, TransferCase Case, List<string> Documents)>();

            foreach (var group in missing.GroupBy(r => (r.TransferCaseId, r.RequiredFromRole)))
            {
                var transferCase = group.First().TransferCase;
                var role = group.Key.RequiredFromRole;
                var roleKey = role.ToString();

                bool remindedRecently = await db.CaseAuditLogs.AnyAsync(a =>
                    a.TransferCaseId == transferCase.Id && a.Action == AuditAction &&
                    a.EntityId == roleKey && a.CreatedAt > recentAfter, ct);
                if (remindedRecently) continue;

                var documents = group.Select(r => r.DocumentType.Name).Distinct().OrderBy(n => n).ToList();
                var list = string.Join(", ", documents);

                await cases.NotifyParticipantsAsync(transferCase,
                    $"Reminder: documents still missing — {transferCase.CaseNumber}",
                    $"Please upload: {list}.", CaseNotificationType.DocumentRequired, role);
                cases.Audit(transferCase.Id, null, AuditAction, "TransferCase", roleKey,
                    $"Reminder sent to the {roleKey.ToLower()} about missing documents: {list}.");

                // The email also reaches a party who has not created a Transfer Service account yet.
                var to = role == CaseParticipantRole.Buyer ? transferCase.BuyerEmail : transferCase.SellerEmail;
                if (!string.IsNullOrWhiteSpace(to)) emails.Add((to, transferCase, documents));
            }

            if (emails.Count == 0) return 0;
            await db.SaveChangesAsync(ct);

            var enc = HtmlEncoder.Default;
            foreach (var (to, c, documents) in emails)
            {
                var link = (publicBaseUrl ?? "").TrimEnd('/') + $"/Cases/Details/{c.Id}";
                try
                {
                    await email.SendEmailAsync(to, $"Case {c.CaseNumber}: documents still missing",
                        $"<p>Your ownership-transfer case <strong>{enc.Encode(c.CaseNumber)}</strong> " +
                        $"({enc.Encode(c.PropertyTitle)}) is waiting for these documents:</p>" +
                        "<ul>" + string.Concat(documents.Select(d => $"<li>{enc.Encode(d)}</li>")) + "</ul>" +
                        $"<p><a href=\"{enc.Encode(link)}\">Open the case</a> to upload them. " +
                        "If you have no account yet, register with this email address.</p>");
                }
                catch (Exception ex)
                {
                    // The in-app reminder and history entry are already saved.
                    logger.LogWarning(ex, "Could not email a document reminder for case {CaseNumber}.", c.CaseNumber);
                }
            }

            return emails.Count;
        }
    }
}
