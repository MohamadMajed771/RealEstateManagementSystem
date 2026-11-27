using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;
using PropertyTransferManagement.Services.Messaging;
using PropertyTransferManagement.ViewModels;
using SharedContracts.Events;

namespace PropertyTransferManagement.Controllers
{
    /// <summary>
    /// Case officer work: dashboard, document review, status updates
    /// and recording the official completion evidence.
    /// Officers only access cases assigned to them; admins access all.
    /// </summary>
    [Authorize(Roles = DataSeeder.OfficerRole + "," + DataSeeder.AdminRole)]
    public class OfficerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CaseService _cases;
        private readonly DocumentStorage _storage;
        private readonly CaseChatService _chat;

        public OfficerController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            CaseService cases,
            DocumentStorage storage,
            CaseChatService chat)
        {
            _db = db;
            _userManager = userManager;
            _cases = cases;
            _storage = storage;
            _chat = chat;
        }

        // GET: /Officer
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;

            var cases = await _db.TransferCases
                .AsNoTracking()
                .Where(c => c.AssignedOfficerId == userId)
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .ToListAsync();

            var caseIds = cases.Select(c => c.Id).ToList();

            ViewBag.PendingReviewByCase = await _db.CaseDocuments
                .AsNoTracking()
                .Where(d => caseIds.Contains(d.TransferCaseId) &&
                            d.Status == CaseDocumentStatus.PendingReview && !d.IsDeleted)
                .GroupBy(d => d.TransferCaseId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            return View(cases);
        }

        // GET: /Officer/Case/{id}
        public async Task<IActionResult> Case(Guid id)
        {
            var transferCase = await LoadAccessibleCaseAsync(id, tracking: false);
            if (transferCase == null) return NotFound();

            var requirements = await _db.CaseDocumentRequirements
                .AsNoTracking()
                .Include(r => r.DocumentType)
                .Where(r => r.TransferCaseId == id)
                .OrderBy(r => r.RequiredFromRole).ThenBy(r => r.Id)
                .ToListAsync();

            await _chat.EnsureConversationsAsync(id);
            await _db.SaveChangesAsync();

            var vm = new OfficerCaseViewModel
            {
                Conversations = await _chat.MyConversationsAsync(id, _userManager.GetUserId(User)!),
                Appointments = await _db.CaseAppointments.AsNoTracking()
                    .Where(a => a.TransferCaseId == id).OrderBy(a => a.StartAt).ToListAsync(),
                LoanRequests = await _db.LoanAssistanceRequests.AsNoTracking()
                    .Include(r => r.RequestedByUser)
                    .Where(r => r.TransferCaseId == id).OrderByDescending(r => r.CreatedAt).ToListAsync(),
                Payments = await _db.ServicePayments.AsNoTracking()
                    .Include(p => p.PaidByUser)
                    .Where(p => p.TransferCaseId == id).OrderByDescending(p => p.CreatedAt).ToListAsync(),

                Case = transferCase,
                Requirements = requirements,
                AllMandatoryApproved = requirements.Where(r => r.IsMandatory).All(r =>
                    r.Status is DocumentRequirementStatus.Approved or DocumentRequirementStatus.Waived),
                NextStatuses = CaseWorkflow.NextStatuses(transferCase.Status),

                Participants = await _db.CaseParticipants
                    .AsNoTracking()
                    .Include(p => p.User)
                    .Where(p => p.TransferCaseId == id && p.IsActive)
                    .ToListAsync(),

                Invitations = await _db.CaseInvitations
                    .AsNoTracking()
                    .Where(i => i.TransferCaseId == id)
                    .ToListAsync(),

                Documents = await _db.CaseDocuments
                    .AsNoTracking()
                    .Include(d => d.DocumentType)
                    .Include(d => d.UploadedByUser)
                    .Include(d => d.Reviews)
                    .Where(d => d.TransferCaseId == id && !d.IsDeleted)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync(),

                History = await _db.CaseStatusHistories
                    .AsNoTracking()
                    .Include(h => h.ChangedByUser)
                    .Where(h => h.TransferCaseId == id)
                    .OrderBy(h => h.ChangedAt)
                    .ToListAsync(),

                AuditLog = await _db.CaseAuditLogs
                    .AsNoTracking()
                    .Include(a => a.User)
                    .Where(a => a.TransferCaseId == id)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(50)
                    .ToListAsync(),

                Confirmation = await _db.OfficialConfirmations
                    .AsNoTracking()
                    .Where(o => o.TransferCaseId == id)
                    .OrderByDescending(o => o.RecordedAt)
                    .FirstOrDefaultAsync()
            };

            return View(vm);
        }

        // POST: /Officer/ReviewDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewDocument(int documentId, DocumentReviewDecision decision, string? comments)
        {
            var userId = _userManager.GetUserId(User)!;

            var document = await _db.CaseDocuments
                .Include(d => d.DocumentType)
                .Include(d => d.CaseDocumentRequirement)
                .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted);

            if (document == null) return NotFound();

            var transferCase = await LoadAccessibleCaseAsync(document.TransferCaseId, tracking: true);
            if (transferCase == null) return NotFound();

            if (document.Status != CaseDocumentStatus.PendingReview)
            {
                TempData["Error"] = "This document was already reviewed.";
                return RedirectToAction(nameof(Case), new { id = transferCase.Id });
            }

            comments = comments?.Trim();
            if (decision != DocumentReviewDecision.Approved && string.IsNullOrWhiteSpace(comments))
            {
                TempData["Error"] = "Please write the reason so the user knows what to correct.";
                return RedirectToAction(nameof(Case), new { id = transferCase.Id });
            }

            var now = DateTime.UtcNow;
            var requirement = document.CaseDocumentRequirement;
            var docName = document.DocumentType.Name;

            _db.DocumentReviews.Add(new DocumentReview
            {
                CaseDocumentId = document.Id,
                ReviewedByUserId = userId,
                Decision = decision,
                Comments = comments,
                ReviewedAt = now
            });

            if (decision == DocumentReviewDecision.Approved)
            {
                document.Status = CaseDocumentStatus.Approved;
                if (requirement != null)
                {
                    requirement.Status = DocumentRequirementStatus.Approved;
                    requirement.SatisfiedAt = now;
                }

                _cases.Notify(document.UploadedByUserId, transferCase.Id,
                    $"{docName} accepted", $"The officer accepted your {docName.ToLower()}.",
                    CaseNotificationType.DocumentApproved);
            }
            else
            {
                document.Status = CaseDocumentStatus.Rejected;
                if (requirement != null)
                    requirement.Status = DocumentRequirementStatus.Rejected;

                var label = decision == DocumentReviewDecision.RequestReplacement ? "Replacement required" : "Rejected";

                _cases.Notify(document.UploadedByUserId, transferCase.Id,
                    $"{docName}: {label}", $"Reason: {comments}. Please upload a corrected document.",
                    CaseNotificationType.DocumentRejected);

                if (transferCase.Status != TransferCaseStatus.AdditionalInformationRequired)
                {
                    await _cases.ChangeStatusAsync(transferCase, TransferCaseStatus.AdditionalInformationRequired,
                        userId, $"{docName}: {comments}");
                }

                var role = requirement?.RequiredFromRole;
                Outbox.Add(_db, new AdditionalDocumentsRequired
                {
                    TransferRequestId = transferCase.MarketplaceTransferRequestId,
                    CaseNumber = transferCase.CaseNumber,
                    RequiredFromBuyer = role == CaseParticipantRole.Buyer,
                    RequiredFromSeller = role == CaseParticipantRole.Seller,
                    DocumentNames = new List<string> { docName },
                    Note = comments
                });
            }

            _cases.Audit(transferCase.Id, userId, "DocumentReviewed", "CaseDocument", document.Id.ToString(),
                $"{docName}: {decision}" + (string.IsNullOrWhiteSpace(comments) ? "" : $" ({comments})"));

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{docName}: {decision}.";
            return RedirectToAction(nameof(Case), new { id = transferCase.Id });
        }

        // POST: /Officer/ChangeStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(Guid id, TransferCaseStatus newStatus, string? reason)
        {
            var userId = _userManager.GetUserId(User)!;

            var transferCase = await LoadAccessibleCaseAsync(id, tracking: true);
            if (transferCase == null) return NotFound();

            reason = reason?.Trim();

            if (!CaseWorkflow.NextStatuses(transferCase.Status).Contains(newStatus))
            {
                TempData["Error"] =
                    $"A case cannot go from '{CaseWorkflow.Display(transferCase.Status)}' to '{CaseWorkflow.Display(newStatus)}'.";
                return RedirectToAction(nameof(Case), new { id });
            }

            if (newStatus is TransferCaseStatus.Rejected or TransferCaseStatus.Cancelled
                    or TransferCaseStatus.OnHold or TransferCaseStatus.AdditionalInformationRequired
                && string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please give a reason for this status.";
                return RedirectToAction(nameof(Case), new { id });
            }

            if (newStatus == TransferCaseStatus.ReadyForProcessing)
            {
                bool allApproved = await _db.CaseDocumentRequirements
                    .Where(r => r.TransferCaseId == id && r.IsMandatory)
                    .AllAsync(r => r.Status == DocumentRequirementStatus.Approved ||
                                   r.Status == DocumentRequirementStatus.Waived);

                if (!allApproved)
                {
                    TempData["Error"] = "All mandatory documents must be accepted first.";
                    return RedirectToAction(nameof(Case), new { id });
                }
            }

            bool isRejection = newStatus == TransferCaseStatus.Rejected;

            await _cases.ChangeStatusAsync(transferCase, newStatus, userId, reason,
                publishStatusChanged: !isRejection);

            if (isRejection)
            {
                Outbox.Add(_db, new TransferRejected
                {
                    TransferRequestId = transferCase.MarketplaceTransferRequestId,
                    CaseNumber = transferCase.CaseNumber,
                    Reason = reason!
                });
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Status changed to '{CaseWorkflow.Display(newStatus)}'.";
            return RedirectToAction(nameof(Case), new { id });
        }

        // POST: /Officer/RecordConfirmation
        // The only way to reach "Official Completion Confirmed":
        // the officer must record the authority, its reference number
        // and the date (optionally with the evidence document).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(DocumentStorage.MaxBytes + 1024 * 1024)]
        public async Task<IActionResult> RecordConfirmation(
            Guid id, string? authorityName, string? referenceNumber, DateTime? confirmationDate, IFormFile? evidence)
        {
            var userId = _userManager.GetUserId(User)!;

            var transferCase = await LoadAccessibleCaseAsync(id, tracking: true);
            if (transferCase == null) return NotFound();

            if (transferCase.Status != TransferCaseStatus.ExternalProcessingInProgress)
            {
                TempData["Error"] = "Official completion can only be recorded during external processing.";
                return RedirectToAction(nameof(Case), new { id });
            }

            authorityName = authorityName?.Trim();
            referenceNumber = referenceNumber?.Trim();

            if (string.IsNullOrWhiteSpace(authorityName) || string.IsNullOrWhiteSpace(referenceNumber) ||
                confirmationDate == null || confirmationDate.Value.Date > DateTime.UtcNow.Date)
            {
                TempData["Error"] = "Authority, official reference number and a past confirmation date are required.";
                return RedirectToAction(nameof(Case), new { id });
            }

            string? evidenceKey = null;
            if (evidence != null && evidence.Length > 0)
            {
                var error = DocumentStorage.Validate(evidence);
                if (error != null)
                {
                    TempData["Error"] = error;
                    return RedirectToAction(nameof(Case), new { id });
                }

                evidenceKey = (await _storage.SaveAsync(id, evidence)).StorageKey;
            }

            var now = DateTime.UtcNow;
            var confirmedAt = DateTime.SpecifyKind(confirmationDate.Value.Date, DateTimeKind.Utc);

            _db.OfficialConfirmations.Add(new OfficialConfirmation
            {
                TransferCaseId = id,
                AuthorityName = authorityName.Length > 200 ? authorityName[..200] : authorityName,
                OfficialReferenceNumber = referenceNumber.Length > 150 ? referenceNumber[..150] : referenceNumber,
                OfficialConfirmationDate = confirmedAt,
                ConfirmationDocumentStorageKey = evidenceKey,
                Status = OfficialConfirmationStatus.Verified,
                RecordedByUserId = userId,
                VerifiedByUserId = userId,
                RecordedAt = now,
                VerifiedAt = now
            });

            await _cases.ChangeStatusAsync(transferCase, TransferCaseStatus.OfficialCompletionConfirmed, userId,
                $"{authorityName} reference {referenceNumber}", publishStatusChanged: false);

            Outbox.Add(_db, new TransferCompleted
            {
                TransferRequestId = transferCase.MarketplaceTransferRequestId,
                CaseNumber = transferCase.CaseNumber,
                AuthorityName = authorityName,
                OfficialReferenceNumber = referenceNumber,
                CompletedAt = confirmedAt
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = "Official completion recorded. The Marketplace has been notified.";
            return RedirectToAction(nameof(Case), new { id });
        }

        private async Task<TransferCase?> LoadAccessibleCaseAsync(Guid id, bool tracking)
        {
            var query = _db.TransferCases.Include(c => c.AssignedOfficer).AsQueryable();
            if (!tracking) query = query.AsNoTracking();

            var transferCase = await query.FirstOrDefaultAsync(c => c.Id == id);
            if (transferCase == null) return null;

            if (User.IsInRole(DataSeeder.AdminRole)) return transferCase;

            return transferCase.AssignedOfficerId == _userManager.GetUserId(User) ? transferCase : null;
        }
    }
}
