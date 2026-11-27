using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;

namespace PropertyTransferManagement.Controllers
{
    /// <summary>
    /// Appointments / administrative steps recorded by the case officer
    /// (spec §29). Buyers and sellers see the ones that concern them.
    /// </summary>
    [Authorize(Roles = DataSeeder.OfficerRole + "," + DataSeeder.AdminRole)]
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly CaseService _cases;
        private readonly CaseAccess _access;
        private readonly CaseEmailer _emailer;
        private readonly UserManager<ApplicationUser> _userManager;

        public AppointmentsController(ApplicationDbContext db, CaseService cases, CaseAccess access,
            CaseEmailer emailer, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _cases = cases;
            _access = access;
            _emailer = emailer;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid caseId, string title, CaseAppointmentType type,
            CaseAppointmentAudience audience, DateTime startAt, int durationMinutes, string? location,
            string? meetingUrl, string? description)
        {
            if (!await _access.IsOfficerAsync(caseId, User)) return Forbid();
            var back = RedirectToAction("Case", "Officer", new { id = caseId });

            title = (title ?? "").Trim();
            var start = DateTime.SpecifyKind(startAt, DateTimeKind.Local).ToUniversalTime();
            if (title.Length is < 3 or > 200 || !Enum.IsDefined(type) || !Enum.IsDefined(audience) ||
                durationMinutes is < 10 or > 480 || start < DateTime.UtcNow.AddMinutes(-5))
            {
                TempData["Error"] = "Check the appointment: a title, a future date and a duration of 10–480 minutes are required.";
                return back;
            }

            if (!string.IsNullOrWhiteSpace(meetingUrl) &&
                !(Uri.TryCreate(meetingUrl.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"))
            {
                TempData["Error"] = "The meeting link must be a web address (https://…).";
                return back;
            }

            var transferCase = await _db.TransferCases.FindAsync(caseId);
            if (transferCase == null) return NotFound();
            if (CaseWorkflow.IsFinished(transferCase.Status))
            {
                TempData["Error"] = "This case is closed; no new appointments can be scheduled.";
                return back;
            }

            var appointment = new CaseAppointment
            {
                TransferCaseId = caseId,
                Title = title,
                AppointmentType = type,
                Audience = audience,
                StartAt = start,
                EndAt = start.AddMinutes(durationMinutes),
                Location = Trim(location, 300),
                MeetingUrl = Trim(meetingUrl, 500),
                Description = Trim(description, 1000),
                Status = CaseAppointmentStatus.Scheduled,
                CreatedByUserId = _userManager.GetUserId(User)
            };
            _db.CaseAppointments.Add(appointment);

            var when = appointment.StartAt.ToLocalTime().ToString("f");
            await _cases.NotifyParticipantsAsync(transferCase,
                $"Appointment scheduled: {title}",
                $"{CaseExtrasDisplay.Appointment(type)} on {when}" +
                (appointment.Location != null ? $" at {appointment.Location}" : "") + ".",
                CaseNotificationType.AppointmentScheduled, CaseExtrasDisplay.AudienceRole(audience));

            _cases.Audit(caseId, appointment.CreatedByUserId, "AppointmentScheduled", "CaseAppointment", null,
                $"{title} ({CaseExtrasDisplay.Appointment(type)}) on {when} for {CaseExtrasDisplay.Audience(audience)}.");

            await _db.SaveChangesAsync();
            await _emailer.SendAppointmentAsync(appointment, transferCase, "An appointment was scheduled for your case");

            TempData["Success"] = "Appointment scheduled and the participants were notified.";
            return back;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, CaseAppointmentStatus status, DateTime? newStartAt, int? durationMinutes, string? note)
        {
            var appointment = await _db.CaseAppointments.FindAsync(id);
            if (appointment == null) return NotFound();
            if (!await _access.IsOfficerAsync(appointment.TransferCaseId, User)) return Forbid();
            var back = RedirectToAction("Case", "Officer", new { id = appointment.TransferCaseId });

            if (!Enum.IsDefined(status) || status == CaseAppointmentStatus.Scheduled)
                return back;

            var transferCase = (await _db.TransferCases.FindAsync(appointment.TransferCaseId))!;
            if (appointment.Status is not (CaseAppointmentStatus.Scheduled or CaseAppointmentStatus.Rescheduled) ||
                CaseWorkflow.IsFinished(transferCase.Status))
            {
                TempData["Error"] = "This appointment can no longer be changed.";
                return back;
            }
            string message;

            if (status == CaseAppointmentStatus.Rescheduled)
            {
                if (newStartAt == null)
                {
                    TempData["Error"] = "Choose the new date and time.";
                    return back;
                }
                var start = DateTime.SpecifyKind(newStartAt.Value, DateTimeKind.Local).ToUniversalTime();
                if (start < DateTime.UtcNow)
                {
                    TempData["Error"] = "The new date and time must be in the future.";
                    return back;
                }
                var minutes = durationMinutes is >= 10 and <= 480 ? durationMinutes.Value : (int)(appointment.EndAt - appointment.StartAt).TotalMinutes;
                appointment.StartAt = start;
                appointment.EndAt = start.AddMinutes(minutes);
                appointment.ReminderSentAt = null;
                message = $"'{appointment.Title}' moved to {start.ToLocalTime():f}.";
            }
            else
            {
                message = $"'{appointment.Title}' is now {status.ToString().ToLowerInvariant()}.";
            }

            appointment.Status = status;
            appointment.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(note)) message += " " + Trim(note, 500);

            await _cases.NotifyParticipantsAsync(transferCase, "Appointment update", message,
                CaseNotificationType.AppointmentChanged, CaseExtrasDisplay.AudienceRole(appointment.Audience));
            _cases.Audit(transferCase.Id, _userManager.GetUserId(User), "Appointment" + status, "CaseAppointment",
                appointment.Id.ToString(), message);

            await _db.SaveChangesAsync();
            if (status is CaseAppointmentStatus.Rescheduled or CaseAppointmentStatus.Cancelled)
                await _emailer.SendAppointmentAsync(appointment, transferCase, "Appointment update: " + message);

            TempData["Success"] = "Appointment updated.";
            return back;
        }

        private static string? Trim(string? value, int max)
        {
            value = value?.Trim();
            if (string.IsNullOrEmpty(value)) return null;
            return value.Length > max ? value[..max] : value;
        }
    }
}
