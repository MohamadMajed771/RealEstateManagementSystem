using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Areas.Admin.Models;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using RealEstateMarketplace.Services;

namespace RealEstateMarketplace.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private const string AdminRole = "Admin";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserModerationService _moderation;

        public UsersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            UserModerationService moderation)
        {
            _context = context;
            _userManager = userManager;
            _moderation = moderation;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? q, string? filter)
        {
            var users = _context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                users = users.Where(u =>
                    u.Email!.Contains(q) || u.FirstName.Contains(q) || u.LastName.Contains(q));
            }

            if (filter == "suspended") users = users.Where(u => !u.IsActive);
            if (filter == "unconfirmed") users = users.Where(u => !u.EmailConfirmed);

            var adminIds = (await _userManager.GetUsersInRoleAsync(AdminRole)).Select(u => u.Id).ToHashSet();

            var rows = await users
                .OrderByDescending(u => u.CreatedAt)
                .Take(500)
                .Select(u => new AdminUserRow
                {
                    Id = u.Id,
                    FullName = u.FirstName + " " + u.LastName,
                    Email = u.Email ?? "",
                    EmailConfirmed = u.EmailConfirmed,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    ListingCount = _context.PropertyListings.Count(l => l.Property.OwnerId == u.Id && !l.IsDeleted),
                    ReportsAgainst = _context.PropertyReports.Count(r => r.PropertyListing.Property.OwnerId == u.Id)
                })
                .ToListAsync();

            foreach (var row in rows) row.IsAdmin = adminIds.Contains(row.Id);

            ViewBag.Query = q;
            ViewBag.Filter = filter;
            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Suspend(string id, string? reason)
        {
            var adminId = _userManager.GetUserId(User)!;
            if (id == adminId)
            {
                TempData["Error"] = "You cannot suspend your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Please give a reason for the suspension.";
                return RedirectToAction(nameof(Index));
            }

            await _moderation.SuspendAsync(user, adminId, reason.Trim());
            TempData["Success"] = $"{user.Email} is suspended. Their approved listings were hidden.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _moderation.ReactivateAsync(user, _userManager.GetUserId(User)!);
            TempData["Success"] = $"{user.Email} can sign in again. Hidden listings stay archived until the owner relists them.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAdmin(string id)
        {
            var adminId = _userManager.GetUserId(User)!;
            if (id == adminId)
            {
                TempData["Error"] = "You cannot change your own admin role.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            bool isAdmin = await _userManager.IsInRoleAsync(user, AdminRole);
            var result = isAdmin
                ? await _userManager.RemoveFromRoleAsync(user, AdminRole)
                : await _userManager.AddToRoleAsync(user, AdminRole);

            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            await _userManager.UpdateSecurityStampAsync(user);
            _moderation.Audit(adminId, isAdmin ? "AdminRoleRemoved" : "AdminRoleGranted", "ApplicationUser", user.Id,
                $"{user.Email}: admin role {(isAdmin ? "removed" : "granted")}.");
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{user.Email} is {(isAdmin ? "no longer" : "now")} an administrator.";
            return RedirectToAction(nameof(Index));
        }
    }
}
