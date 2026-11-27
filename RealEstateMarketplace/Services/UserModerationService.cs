using Microsoft.AspNetCore.Identity;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Services
{
    /// <summary>
    /// Suspends / reactivates accounts and writes admin audit entries.
    /// Suspension uses Identity lockout, so the login page refuses the
    /// account, and the security stamp change signs out open sessions.
    /// </summary>
    public class UserModerationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserModerationService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task SuspendAsync(ApplicationUser user, string adminId, string reason)
        {
            user.IsActive = false;
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await _userManager.UpdateSecurityStampAsync(user);

            // Suspended owners' listings are hidden from the public.
            var listings = _context.PropertyListings
                .Where(l => l.Property.OwnerId == user.Id && l.Status == ListingStatus.Approved && !l.IsDeleted)
                .ToList();

            foreach (var listing in listings)
            {
                listing.Status = ListingStatus.Archived;
                listing.UpdatedAt = DateTime.UtcNow;
            }

            Audit(adminId, "UserSuspended", "ApplicationUser", user.Id, reason);
            await _context.SaveChangesAsync();
        }

        public async Task ReactivateAsync(ApplicationUser user, string adminId)
        {
            user.IsActive = true;
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            Audit(adminId, "UserReactivated", "ApplicationUser", user.Id, "Account reactivated.");
            await _context.SaveChangesAsync();
        }

        public void Audit(string? actorId, string action, string entityType, string? entityId, string description)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = actorId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description.Length > 2000 ? description[..2000] : description,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
