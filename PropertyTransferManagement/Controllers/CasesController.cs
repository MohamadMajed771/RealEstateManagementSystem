using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;
using PropertyTransferManagement.ViewModels;

namespace PropertyTransferManagement.Controllers
{
    /// <summary>
    /// Buyer / seller side: my cases, document checklist and uploads.
    /// A user only sees cases where they are an active participant.
    /// </summary>
    [Authorize]
    public class CasesController : Controller
    {
        private static readonly TransferCaseStatus[] UploadStatuses =
        {
            TransferCaseStatus.RequestReceived,
            TransferCaseStatus.AwaitingDocuments,
            TransferCaseStatus.DocumentsUnderReview,
            TransferCaseStatus.AdditionalInformationRequired,
            TransferCaseStatus.OnHold
        };

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CaseService _cases;
        private readonly DocumentStorage _storage;

        public CasesController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            CaseService cases,
            DocumentStorage storage)
        {
            _db = db;
            _userManager = userManager;
            _cases = cases;
            _storage = storage;
        }

        // GET: /Cases
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var linked = await _cases.ClaimInvitationsAsync(user);
            if (linked > 0)
                TempData["Success"] = $"{linked} transfer case(s) were linked to your account.";

            var participations = await _db.CaseParticipants
                .AsNoTracking()
                .Include(p => p.TransferCase).ThenInclude(c => c.AssignedOfficer)
                .Where(p => p.UserId == user.Id && p.IsActive)
                .ToListAsync();

            var caseIds = participations.Select(p => p.TransferCaseId).Distinct().ToList();

            var openRequirements = await _db.CaseDocumentRequirements
                .AsNoTracking()
                .Where(r => caseIds.Contains(r.TransferCaseId) &&
                            (r.Status == DocumentRequirementStatus.Pending ||
                             r.Status == DocumentRequirementStatus.Rejected))
                .ToListAsync();

            var rows = participations
                .GroupBy(p => p.TransferCaseId)
                .Select(g =>
                {
                    var c = g.First().TransferCase;
                    var roles = g.Select(p => p.Role).ToList();
                    return new MyCaseRow
                    {
                        Case = c,
                        MyRoles = roles,
                        OfficerName = c.AssignedOfficer == null ? null : FullName(c.AssignedOfficer),
                        DocumentsToUpload = openRequirements.Count(r =>
                            r.TransferCaseId == c.Id && roles.Contains(r.RequiredFromRole))
                    };
                })
                .OrderByDescending(r => r.Case.CreatedAt)
                .ToList();

            return View(rows);
        }

        // GET: /Cases/Details/{id}
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = _userManager.GetUserId(User)!;

            var roles = await MyRolesAsync(id, userId);
            if (roles.Count == 0) return NotFound();

            var transferCase = await _db.TransferCases
                .AsNoTracking()
                .Include(c => c.AssignedOfficer)
                .FirstAsync(c => c.Id == id);

            var vm = new CaseDetailsViewModel
            {
                Case = transferCase,
                MyRoles = roles,
                OfficerName = transferCase.AssignedOfficer == null ? null : FullName(transferCase.AssignedOfficer),
                CanUpload = UploadStatuses.Contains(transferCase.Status),

                MyRequirements = await _db.CaseDocumentRequirements
                    .AsNoTracking()
                    .Include(r => r.DocumentType)
                    .Where(r => r.TransferCaseId == id && roles.Contains(r.RequiredFromRole))
                    .OrderBy(r => r.Id)
                    .ToListAsync(),

                // Each party only sees their own uploads.
                MyDocuments = await _db.CaseDocuments
                    .AsNoTracking()
                    .Include(d => d.DocumentType)
                    .Include(d => d.Reviews)
                    .Where(d => d.TransferCaseId == id && d.UploadedByUserId == userId && !d.IsDeleted)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync(),

                History = await _db.CaseStatusHistories
                    .AsNoTracking()
                    .Where(h => h.TransferCaseId == id)
                    .OrderBy(h => h.ChangedAt)
                    .ToListAsync()
            };

            return View(vm);
        }

        // POST: /Cases/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(DocumentStorage.MaxBytes + 1024 * 1024)]
        public async Task<IActionResult> Upload(Guid caseId, int requirementId, IFormFile? file)
        {
            var userId = _userManager.GetUserId(User)!;

            var roles = await MyRolesAsync(caseId, userId);
            if (roles.Count == 0) return NotFound();

            var transferCase = await _db.TransferCases.FirstAsync(c => c.Id == caseId);

            var requirement = await _db.CaseDocumentRequirements
                .Include(r => r.DocumentType)
                .FirstOrDefaultAsync(r => r.Id == requirementId && r.TransferCaseId == caseId);

            if (requirement == null || !roles.Contains(requirement.RequiredFromRole))
                return Forbid();

            if (!UploadStatuses.Contains(transferCase.Status))
            {
                TempData["Error"] = "Documents can no longer be uploaded for this case.";
                return RedirectToAction(nameof(Details), new { id = caseId });
            }

            if (requirement.Status == DocumentRequirementStatus.Approved)
            {
                TempData["Error"] = "This document was already accepted.";
                return RedirectToAction(nameof(Details), new { id = caseId });
            }

            var error = DocumentStorage.Validate(file);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(nameof(Details), new { id = caseId });
            }

            var stored = await _storage.SaveAsync(caseId, file!);
            var now = DateTime.UtcNow;

            // Older submissions waiting for review are replaced.
            var previous = await _db.CaseDocuments
                .Where(d => d.CaseDocumentRequirementId == requirementId && !d.IsDeleted)
                .ToListAsync();

            foreach (var old in previous.Where(d => d.Status == CaseDocumentStatus.PendingReview))
                old.Status = CaseDocumentStatus.Superseded;

            var document = new CaseDocument
            {
                TransferCaseId = caseId,
                DocumentTypeId = requirement.DocumentTypeId,
                CaseDocumentRequirementId = requirementId,
                UploadedByUserId = userId,
                StorageKey = stored.StorageKey,
                OriginalFileName = stored.OriginalFileName,
                ContentType = stored.ContentType,
                FileSizeBytes = stored.SizeBytes,
                Sha256Hash = stored.Sha256,
                Status = CaseDocumentStatus.PendingReview,
                Version = previous.Count + 1,
                UploadedAt = now
            };

            _db.CaseDocuments.Add(document);

            requirement.Status = DocumentRequirementStatus.Submitted;

            _cases.Audit(caseId, userId, "DocumentUploaded", "CaseDocument", null,
                $"{requirement.DocumentType.Name} uploaded (version {document.Version}).");

            if (transferCase.AssignedOfficerId != null)
            {
                _cases.Notify(transferCase.AssignedOfficerId, caseId,
                    $"New document on {transferCase.CaseNumber}",
                    $"{requirement.DocumentType.Name} was submitted and is waiting for review.",
                    CaseNotificationType.DocumentRequired);
            }

            // Move the case forward automatically when everything is in.
            var allRequirements = await _db.CaseDocumentRequirements
                .Where(r => r.TransferCaseId == caseId && r.IsMandatory)
                .ToListAsync();

            bool allSubmitted = allRequirements.All(r =>
                r.Status is DocumentRequirementStatus.Submitted
                    or DocumentRequirementStatus.Approved
                    or DocumentRequirementStatus.Waived);

            if (allSubmitted && transferCase.Status is TransferCaseStatus.RequestReceived
                    or TransferCaseStatus.AwaitingDocuments
                    or TransferCaseStatus.AdditionalInformationRequired)
            {
                await _cases.ChangeStatusAsync(transferCase, TransferCaseStatus.DocumentsUnderReview,
                    userId, "All required documents have been submitted.");
            }
            else if (transferCase.Status == TransferCaseStatus.RequestReceived)
            {
                await _cases.ChangeStatusAsync(transferCase, TransferCaseStatus.AwaitingDocuments,
                    userId, "Document submission started.");
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{requirement.DocumentType.Name} uploaded. The officer will review it.";
            return RedirectToAction(nameof(Details), new { id = caseId });
        }

        // GET: /Cases/Download/{id}
        // Uploader, the assigned officer and admins only.
        public async Task<IActionResult> Download(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var document = await _db.CaseDocuments
                .AsNoTracking()
                .Include(d => d.TransferCase)
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (document == null) return NotFound();

            bool allowed =
                document.UploadedByUserId == userId ||
                document.TransferCase.AssignedOfficerId == userId ||
                User.IsInRole(DataSeeder.AdminRole);

            if (!allowed) return NotFound();

            _cases.Audit(document.TransferCaseId, userId, "DocumentViewed", "CaseDocument",
                document.Id.ToString(), "Document opened.");
            await _db.SaveChangesAsync();

            return PhysicalFile(_storage.GetFullPath(document.StorageKey), document.ContentType);
        }

        private Task<List<CaseParticipantRole>> MyRolesAsync(Guid caseId, string userId) =>
            _db.CaseParticipants
                .Where(p => p.TransferCaseId == caseId && p.UserId == userId && p.IsActive)
                .Select(p => p.Role)
                .ToListAsync();

        private static string FullName(ApplicationUser user) =>
            $"{user.FirstName} {user.LastName}".Trim() is { Length: > 0 } name ? name : user.Email ?? "";
    }
}
