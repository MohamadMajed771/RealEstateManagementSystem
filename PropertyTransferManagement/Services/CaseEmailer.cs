using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Services
{
    /// <summary>Email copies of important case events (appointments).</summary>
    public class CaseEmailer
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _email;
        private readonly IConfiguration _config;
        private readonly ILogger<CaseEmailer> _logger;

        public CaseEmailer(ApplicationDbContext db, IEmailSender email, IConfiguration config, ILogger<CaseEmailer> logger)
        {
            _db = db;
            _email = email;
            _config = config;
            _logger = logger;
        }

        public async Task SendAppointmentAsync(CaseAppointment a, TransferCase c, string headline)
        {
            var role = CaseExtrasDisplay.AudienceRole(a.Audience);
            var emails = await _db.CaseParticipants
                .Where(p => p.TransferCaseId == c.Id && p.IsActive && (role == null || p.Role == role))
                .Select(p => p.User.Email)
                .Distinct()
                .ToListAsync();

            var enc = HtmlEncoder.Default;
            var link = (_config["Transfer:PublicBaseUrl"] ?? "").TrimEnd('/') + $"/Cases/Details/{c.Id}";
            var body =
                $"<p>{enc.Encode(headline)}</p>" +
                $"<p><strong>{enc.Encode(a.Title)}</strong> — {enc.Encode(CaseExtrasDisplay.Appointment(a.AppointmentType))}<br/>" +
                $"{a.StartAt.ToLocalTime():f} – {a.EndAt.ToLocalTime():t}<br/>" +
                (a.Location != null ? $"Place: {enc.Encode(a.Location)}<br/>" : "") +
                (a.MeetingUrl != null ? $"Online: {enc.Encode(a.MeetingUrl)}<br/>" : "") +
                $"Status: {a.Status}</p>" +
                (a.Description != null ? $"<p>{enc.Encode(a.Description)}</p>" : "") +
                $"<p>Case {enc.Encode(c.CaseNumber)} — <a href=\"{enc.Encode(link)}\">open the case</a></p>";

            foreach (var to in emails.Where(e => !string.IsNullOrWhiteSpace(e)))
            {
                try
                {
                    await _email.SendEmailAsync(to!, $"Case {c.CaseNumber}: {a.Title}", body);
                }
                catch (Exception ex)
                {
                    // Email is a copy; the in-app notification already exists.
                    _logger.LogWarning(ex, "Could not email appointment {AppointmentId}.", a.Id);
                }
            }
        }
    }

    /// <summary>Sends a reminder (in-app + email) about 24 hours before each appointment.</summary>
    public class AppointmentReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AppointmentReminderService> _logger;

        public AppointmentReminderService(IServiceScopeFactory scopes, ILogger<AppointmentReminderService> logger)
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
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var cases = scope.ServiceProvider.GetRequiredService<CaseService>();
                    var emailer = scope.ServiceProvider.GetRequiredService<CaseEmailer>();

                    var now = DateTime.UtcNow;
                    var due = await db.CaseAppointments
                        .Include(a => a.TransferCase)
                        .Where(a => (a.Status == CaseAppointmentStatus.Scheduled || a.Status == CaseAppointmentStatus.Rescheduled) &&
                                    a.ReminderSentAt == null && a.StartAt > now && a.StartAt <= now.AddHours(24))
                        .ToListAsync(stoppingToken);

                    foreach (var a in due)
                    {
                        a.ReminderSentAt = now;
                        await cases.NotifyParticipantsAsync(a.TransferCase, $"Reminder: {a.Title}",
                            $"{CaseExtrasDisplay.Appointment(a.AppointmentType)} on {a.StartAt.ToLocalTime():f}.",
                            CaseNotificationType.AppointmentScheduled, CaseExtrasDisplay.AudienceRole(a.Audience));
                    }

                    if (due.Count > 0)
                    {
                        await db.SaveChangesAsync(stoppingToken);
                        foreach (var a in due)
                            await emailer.SendAppointmentAsync(a, a.TransferCase, "Reminder: your appointment is coming up");
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Appointment reminder run failed; retrying later.");
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }
    }
}
