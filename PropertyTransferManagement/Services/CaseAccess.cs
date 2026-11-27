using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Services
{
    /// <summary>Who may do what on a case.</summary>
    public class CaseAccess
    {
        private readonly ApplicationDbContext _db;

        public CaseAccess(ApplicationDbContext db) => _db = db;

        public Task<List<CaseParticipantRole>> PartyRolesAsync(Guid caseId, string userId) =>
            _db.CaseParticipants
                .Where(p => p.TransferCaseId == caseId && p.UserId == userId && p.IsActive)
                .Select(p => p.Role)
                .ToListAsync();

        /// <summary>Assigned officer of the case, or any administrator.</summary>
        public async Task<bool> IsOfficerAsync(Guid caseId, ClaimsPrincipal user)
        {
            if (user.IsInRole(DataSeeder.AdminRole)) return true;
            if (!user.IsInRole(DataSeeder.OfficerRole)) return false;
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return await _db.TransferCases.AnyAsync(c => c.Id == caseId && c.AssignedOfficerId == userId);
        }
    }
}
