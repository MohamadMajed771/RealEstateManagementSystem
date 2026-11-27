using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;

namespace PropertyTransferManagement.Controllers
{
    /// <summary>
    /// Housing-loan assistance (spec §30). The service only organises the
    /// file; it never approves loans. Lender decisions are only recorded.
    /// </summary>
    [Authorize]
    public class LoansController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly CaseService _cases;
        private readonly CaseAccess _access;
        private readonly UserManager<ApplicationUser> _userManager;

        public LoansController(ApplicationDbContext db, CaseService cases, CaseAccess access, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _cases = cases;
            _access = access;
            _userManager = userManager;
        }

        // Buyer: submit a request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(Guid caseId, decimal requestedAmount, string? preferredBank, string? additionalInformation)
        {
            var userId = _userManager.GetUserId(User)!;
            var back = RedirectToAction("Details", "Cases", new { id = caseId });

            var roles = await _access.PartyRolesAsync(caseId, userId);
            if (!roles.Contains(CaseParticipantRole.Buyer)) return Forbid();

            var transferCase = await _db.TransferCases.FindAsync(caseId);
            if (transferCase == null) return NotFound();

            if (CaseWorkflow.IsFinished(transferCase.Status))
            {
                TempData["Error"] = "This case is closed.";
                return back;
            }

            if (requestedAmount <= 0 || requestedAmount > transferCase.AgreedPrice)
            {
                TempData["Error"] = $"The amount must be more than 0 and not more than the agreed price ({transferCase.AgreedPrice:N0} {transferCase.Currency}).";
                return back;
            }

            bool hasOpen = await _db.LoanAssistanceRequests.AnyAsync(r =>
                r.TransferCaseId == caseId && r.RequestedByUserId == userId &&
                (r.Status == LoanAssistanceStatus.Pending || r.Status == LoanAssistanceStatus.UnderReview ||
                 r.Status == LoanAssistanceStatus.InformationRequested || r.Status == LoanAssistanceStatus.ReferredToBank));
            if (hasOpen)
            {
                TempData["Error"] = "You already have an open loan-assistance request for this case.";
                return back;
            }

            var request = new LoanAssistanceRequest
            {
                TransferCaseId = caseId,
                RequestedByUserId = userId,
                RequestedAmount = requestedAmount,
                Currency = transferCase.Currency,
                PreferredBank = Trim(preferredBank, 200),
                AdditionalInformation = Trim(additionalInformation, 2000),
                Status = LoanAssistanceStatus.Pending
            };
            _db.LoanAssistanceRequests.Add(request);

            if (transferCase.AssignedOfficerId != null)
                _cases.Notify(transferCase.AssignedOfficerId, caseId, $"Loan assistance requested — {transferCase.CaseNumber}",
                    $"The buyer asks for help financing {requestedAmount:N0} {transferCase.Currency}.", CaseNotificationType.SystemAnnouncement);
            _cases.Audit(caseId, userId, "LoanAssistanceRequested", "LoanAssistanceRequest", null,
                $"Requested {requestedAmount:N0} {transferCase.Currency}.");

            await _db.SaveChangesAsync();
            TempData["Success"] = "Your loan-assistance request was submitted. The officer will review it.";
            return back;
        }

        // Buyer: answer an information request, or cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Respond(int id, string? additionalInformation, bool cancel = false)
        {
            var userId = _userManager.GetUserId(User)!;
            var request = await _db.LoanAssistanceRequests.Include(r => r.TransferCase).FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();
            if (request.RequestedByUserId != userId) return Forbid();
            var back = RedirectToAction("Details", "Cases", new { id = request.TransferCaseId });

            if (!CaseExtrasDisplay.LoanIsOpen(request.Status)) return back;

            if (cancel)
            {
                request.Status = LoanAssistanceStatus.Cancelled;
            }
            else
            {
                var info = Trim(additionalInformation, 1000);
                if (info == null)
                {
                    TempData["Error"] = "Please write the requested information.";
                    return back;
                }
                var combined = (request.AdditionalInformation + $"\n[{DateTime.Now:g}] " + info).Trim();
                request.AdditionalInformation = combined.Length > 2000 ? combined[^2000..] : combined;
                if (request.Status == LoanAssistanceStatus.InformationRequested)
                    request.Status = LoanAssistanceStatus.UnderReview;
            }
            request.UpdatedAt = DateTime.UtcNow;

            if (request.TransferCase.AssignedOfficerId != null)
                _cases.Notify(request.TransferCase.AssignedOfficerId, request.TransferCaseId,
                    $"Loan request {(cancel ? "cancelled" : "updated")} — {request.TransferCase.CaseNumber}",
                    cancel ? "The buyer cancelled the loan-assistance request." : "The buyer added information to the loan-assistance request.",
                    CaseNotificationType.SystemAnnouncement);
            _cases.Audit(request.TransferCaseId, userId, cancel ? "LoanAssistanceCancelled" : "LoanAssistanceInfoAdded",
                "LoanAssistanceRequest", request.Id.ToString(), cancel ? "Cancelled by the buyer." : "Buyer added information.");

            await _db.SaveChangesAsync();
            TempData["Success"] = cancel ? "Request cancelled." : "Information sent to the officer.";
            return back;
        }

        // Officer: move the request forward
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = DataSeeder.OfficerRole + "," + DataSeeder.AdminRole)]
        public async Task<IActionResult> Review(int id, LoanAssistanceStatus status, string? notes, string? lenderName, LenderDecision? lenderDecision)
        {
            var request = await _db.LoanAssistanceRequests.Include(r => r.TransferCase).FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();
            if (!await _access.IsOfficerAsync(request.TransferCaseId, User)) return Forbid();
            var back = RedirectToAction("Case", "Officer", new { id = request.TransferCaseId });

            if (!CaseExtrasDisplay.LoanIsOpen(request.Status) || CaseWorkflow.IsFinished(request.TransferCase.Status))
            {
                TempData["Error"] = "This loan-assistance request is already finished and cannot be changed.";
                return back;
            }

            notes = Trim(notes, 2000);
            lenderName = Trim(lenderName, 200);

            string? error = status switch
            {
                _ when !Enum.IsDefined(status) || status == LoanAssistanceStatus.Pending => "Choose a new status.",
                LoanAssistanceStatus.InformationRequested when notes == null => "Say which information or documents are missing.",
                LoanAssistanceStatus.ReferredToBank when lenderName == null && request.LenderName == null => "Enter the lender the file is sent to.",
                LoanAssistanceStatus.Closed when lenderDecision == null => "Record the lender's decision.",
                LoanAssistanceStatus.Rejected when notes == null => "Explain why the request is not taken forward.",
                _ => null
            };
            if (error != null)
            {
                TempData["Error"] = error;
                return back;
            }

            var now = DateTime.UtcNow;
            request.Status = status;
            request.ReviewNotes = notes ?? request.ReviewNotes;
            request.LenderName = lenderName ?? request.LenderName;
            if (status == LoanAssistanceStatus.Closed)
            {
                request.LenderDecision = lenderDecision;
                request.LenderDecisionAt = now;
            }
            request.ReviewedByUserId = _userManager.GetUserId(User);
            request.ReviewedAt = now;
            request.UpdatedAt = now;

            var text = $"Loan assistance: {CaseExtrasDisplay.Loan(status)}" +
                       (status == LoanAssistanceStatus.Closed ? $" — {CaseExtrasDisplay.Lender(lenderDecision)}" : "") +
                       (request.LenderName != null && status is LoanAssistanceStatus.ReferredToBank or LoanAssistanceStatus.Closed ? $" ({request.LenderName})" : "") +
                       (notes != null ? $". {notes}" : ".");

            _cases.Notify(request.RequestedByUserId, request.TransferCaseId, $"Loan assistance update — {request.TransferCase.CaseNumber}",
                text, CaseNotificationType.SystemAnnouncement);
            _cases.Audit(request.TransferCaseId, request.ReviewedByUserId, "LoanAssistance" + status, "LoanAssistanceRequest",
                request.Id.ToString(), text);

            await _db.SaveChangesAsync();
            TempData["Success"] = "Loan-assistance request updated.";
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
