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
    /// System administrator: case queue, officer assignment,
    /// statistics and officer accounts.
    /// </summary>
    [Authorize(Roles = DataSeeder.AdminRole)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CaseService _cases;

        public AdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            CaseService cases)
        {
            _db = db;
            _userManager = userManager;
            _cases = cases;
        }

        // GET: /Admin
        public async Task<IActionResult> Index()
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var cases = _db.TransferCases.AsNoTracking();

            var vm = new AdminDashboardViewModel
            {
                TotalCases = await cases.CountAsync(),
                NewThisMonth = await cases.CountAsync(c => c.CreatedAt >= monthStart),
                Unassigned = await cases.CountAsync(c => c.AssignedOfficerId == null),
                AwaitingDocuments = await cases.CountAsync(c =>
                    c.Status == TransferCaseStatus.AwaitingDocuments ||
                    c.Status == TransferCaseStatus.AdditionalInformationRequired),
                UnderReview = await cases.CountAsync(c => c.Status == TransferCaseStatus.DocumentsUnderReview),
                Completed = await cases.CountAsync(c =>
                    c.Status == TransferCaseStatus.OfficialCompletionConfirmed ||
                    c.Status == TransferCaseStatus.CaseClosed),
                CancelledOrRejected = await cases.CountAsync(c =>
                    c.Status == TransferCaseStatus.Cancelled || c.Status == TransferCaseStatus.Rejected),

                Cases = await cases
                    .Include(c => c.AssignedOfficer)
                    .OrderBy(c => c.AssignedOfficerId != null)
                    .ThenByDescending(c => c.CreatedAt)
                    .ToListAsync(),

                Officers = await OfficerOptionsAsync()
            };

            return View(vm);
        }

        // POST: /Admin/Assign
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(Guid caseId, string officerId)
        {
            var adminId = _userManager.GetUserId(User)!;

            var transferCase = await _db.TransferCases.FirstOrDefaultAsync(c => c.Id == caseId);
            if (transferCase == null) return NotFound();

            var officer = await _userManager.FindByIdAsync(officerId);
            if (officer == null || !officer.IsActive ||
                !await _userManager.IsInRoleAsync(officer, DataSeeder.OfficerRole))
            {
                TempData["Error"] = "Please choose an active officer.";
                return RedirectToAction(nameof(Index));
            }

            if (transferCase.AssignedOfficerId == officerId)
            {
                TempData["Error"] = "This officer is already assigned to the case.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.UtcNow;
            bool isReassignment = transferCase.AssignedOfficerId != null;

            var active = await _db.OfficerAssignments
                .Where(a => a.TransferCaseId == caseId && a.Status == OfficerAssignmentStatus.Active)
                .ToListAsync();

            foreach (var a in active)
            {
                a.Status = OfficerAssignmentStatus.Reassigned;
                a.UnassignedAt = now;
            }

            _db.OfficerAssignments.Add(new OfficerAssignment
            {
                TransferCaseId = caseId,
                OfficerId = officerId,
                AssignedByUserId = adminId,
                AssignedAt = now
            });

            transferCase.AssignedOfficerId = officerId;
            transferCase.UpdatedAt = now;

            var officerName = $"{officer.FirstName} {officer.LastName}".Trim();

            _cases.Notify(officerId, caseId,
                $"Case {transferCase.CaseNumber} assigned to you",
                $"{transferCase.PropertyTitle} — {transferCase.AgreedPrice:N0} {transferCase.Currency}.",
                CaseNotificationType.OfficerAssigned);

            await _cases.NotifyParticipantsAsync(transferCase,
                $"Officer assigned to {transferCase.CaseNumber}",
                $"{officerName} is now handling your case.",
                CaseNotificationType.OfficerAssigned);

            _cases.Audit(caseId, adminId, isReassignment ? "OfficerReassigned" : "OfficerAssigned",
                "TransferCase", caseId.ToString(), $"Assigned to {officerName}.");

            if (transferCase.Status == TransferCaseStatus.RequestReceived)
            {
                await _cases.ChangeStatusAsync(transferCase, TransferCaseStatus.AwaitingDocuments, adminId,
                    "Officer assigned. Buyer and seller can upload their documents.");
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{transferCase.CaseNumber} assigned to {officerName}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Officers
        public async Task<IActionResult> Officers()
        {
            return View(await OfficerOptionsAsync());
        }

        // POST: /Admin/CreateOfficer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOfficer(
            string firstName, string lastName, string email, string? employeeNumber, string password)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "First name, last name, email and password are required.";
                return RedirectToAction(nameof(Officers));
            }

            var officer = new ApplicationUser
            {
                UserName = email.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true, // created by an administrator
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                EmployeeNumber = employeeNumber?.Trim(),
                JobTitle = "Case Officer"
            };

            var result = await _userManager.CreateAsync(officer, password);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Officers));
            }

            await _userManager.AddToRoleAsync(officer, DataSeeder.OfficerRole);

            _cases.Audit(null, _userManager.GetUserId(User), "OfficerCreated", "ApplicationUser",
                officer.Id, $"Officer account created for {officer.Email}.");
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Officer {officer.FirstName} {officer.LastName} created.";
            return RedirectToAction(nameof(Officers));
        }

        private async Task<List<OfficerOption>> OfficerOptionsAsync()
        {
            var officers = await _userManager.GetUsersInRoleAsync(DataSeeder.OfficerRole);

            var load = await _db.TransferCases
                .Where(c => c.AssignedOfficerId != null &&
                            c.Status != TransferCaseStatus.CaseClosed &&
                            c.Status != TransferCaseStatus.Cancelled &&
                            c.Status != TransferCaseStatus.Rejected)
                .GroupBy(c => c.AssignedOfficerId!)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return officers
                .Where(o => o.IsActive)
                .Select(o => new OfficerOption
                {
                    Id = o.Id,
                    Name = $"{o.FirstName} {o.LastName} ({o.Email})",
                    ActiveCases = load.GetValueOrDefault(o.Id)
                })
                .OrderBy(o => o.Name)
                .ToList();
        }
    }
}
