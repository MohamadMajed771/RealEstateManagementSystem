using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;

            var notifications = await _db.CaseNotifications
                .AsNoTracking()
                .Where(n => n.UserId == userId && !n.IsDeleted)
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .ToListAsync();

            return View(notifications);
        }

        // Opens the notification target and marks it as read.
        public async Task<IActionResult> Open(long id)
        {
            var userId = _userManager.GetUserId(User)!;

            var notification = await _db.CaseNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null) return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // Officers open the officer view of the case.
            if (notification.TransferCaseId != null &&
                (User.IsInRole(DataSeeder.OfficerRole) || User.IsInRole(DataSeeder.AdminRole)))
            {
                return RedirectToAction("Case", "Officer", new { id = notification.TransferCaseId });
            }

            return Url.IsLocalUrl(notification.ActionUrl)
                ? LocalRedirect(notification.ActionUrl!)
                : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = _userManager.GetUserId(User)!;
            var now = DateTime.UtcNow;

            await _db.CaseNotifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now));

            return RedirectToAction(nameof(Index));
        }
    }
}
