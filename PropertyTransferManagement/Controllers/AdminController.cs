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
            var officers = await _userManager.GetUsersInRoleAsync(DataSeeder.OfficerRole);
            var admins = await _userManager.GetUsersInRoleAsync(DataSeeder.AdminRole);
            var load = await ActiveCaseCountsAsync();

            var staff = officers.Concat(admins)
                .DistinctBy(u => u.Id)
                .Select(u => new StaffMember
                {
                    Id = u.Id,
                    Name = $"{u.FirstName} {u.LastName}",
                    Email = u.Email ?? "",
                    IsActive = u.IsActive,
                    IsOfficer = officers.Any(o => o.Id == u.Id),
                    IsAdmin = admins.Any(a => a.Id == u.Id),
                    ActiveCases = load.GetValueOrDefault(u.Id)
                })
                .OrderBy(s => s.Name)
                .ToList();

            return View(staff);
        }

        // POST: /Admin/SetActive  (deactivate / reactivate a staff account)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(string userId, bool active)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (!active)
            {
                if (user.Id == _userManager.GetUserId(User))
                    return Refuse("You cannot deactivate your own account.");
                var cases = (await ActiveCaseCountsAsync()).GetValueOrDefault(user.Id);
                if (cases > 0)
                    return Refuse($"{user.FirstName} {user.LastName} still has {cases} active case(s). Reassign them first.");
                if (await IsLastActiveAdminAsync(user))
                    return Refuse("There must always be at least one active administrator.");
            }

            user.IsActive = active;
            await _userManager.UpdateAsync(user);
            // A new security stamp signs a deactivated user out within a minute.
            if (!active) await _userManager.UpdateSecurityStampAsync(user);

            _cases.Audit(null, _userManager.GetUserId(User), active ? "AccountReactivated" : "AccountDeactivated",
                "ApplicationUser", user.Id, $"{user.Email} {(active ? "reactivated" : "deactivated")}.");
            await _db.SaveChangesAsync();

            TempData["Success"] = $"{user.FirstName} {user.LastName} was {(active ? "reactivated" : "deactivated")}.";
            return RedirectToAction(nameof(Officers));
        }

        // POST: /Admin/ChangeRole  (give or take the Officer / Admin role)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(string email, string role, bool grant)
        {
            if (role != DataSeeder.OfficerRole && role != DataSeeder.AdminRole) return BadRequest();

            var user = await _userManager.FindByEmailAsync((email ?? "").Trim());
            if (user == null)
                return Refuse("No account uses this email. The person must register first, or create an officer account below.");
            if (grant && !user.EmailConfirmed)
                return Refuse("This account has not verified its email yet.");

            bool has = await _userManager.IsInRoleAsync(user, role);
            if (grant == has)
                return Refuse($"{user.Email} {(has ? "already has" : "does not have")} the {role} role.");

            if (!grant)
            {
                if (role == DataSeeder.AdminRole && user.Id == _userManager.GetUserId(User))
                    return Refuse("You cannot remove your own administrator role.");
                if (role == DataSeeder.AdminRole && await IsLastActiveAdminAsync(user))
                    return Refuse("There must always be at least one active administrator.");
                var cases = (await ActiveCaseCountsAsync()).GetValueOrDefault(user.Id);
                if (role == DataSeeder.OfficerRole && cases > 0)
                    return Refuse($"{user.FirstName} {user.LastName} still has {cases} active case(s). Reassign them first.");
            }

            var result = grant
                ? await _userManager.AddToRoleAsync(user, role)
                : await _userManager.RemoveFromRoleAsync(user, role);
            if (!result.Succeeded)
                return Refuse(string.Join(" ", result.Errors.Select(e => e.Description)));
            await _userManager.UpdateSecurityStampAsync(user); // new roles apply at the next request check

            _cases.Audit(null, _userManager.GetUserId(User), grant ? "RoleGranted" : "RoleRemoved",
                "ApplicationUser", user.Id, $"{role} role {(grant ? "given to" : "removed from")} {user.Email}.");
            await _db.SaveChangesAsync();

            TempData["Success"] = $"{role} role {(grant ? "given to" : "removed from")} {user.FirstName} {user.LastName}.";
            return RedirectToAction(nameof(Officers));
        }

        private IActionResult Refuse(string message)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(Officers));
        }

        private async Task<bool> IsLastActiveAdminAsync(ApplicationUser user)
        {
            if (!await _userManager.IsInRoleAsync(user, DataSeeder.AdminRole)) return false;
            var admins = await _userManager.GetUsersInRoleAsync(DataSeeder.AdminRole);
            return admins.Count(a => a.IsActive && a.Id != user.Id) == 0;
        }

        private Task<Dictionary<string, int>> ActiveCaseCountsAsync() =>
            _db.TransferCases
                .Where(c => c.AssignedOfficerId != null &&
                            c.Status != TransferCaseStatus.CaseClosed &&
                            c.Status != TransferCaseStatus.Cancelled &&
                            c.Status != TransferCaseStatus.Rejected)
                .GroupBy(c => c.AssignedOfficerId!)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

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
            var load = await ActiveCaseCountsAsync();

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
