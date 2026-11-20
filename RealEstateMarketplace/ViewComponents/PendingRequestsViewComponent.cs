
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;
using System.Security.Claims;

namespace RealEstateMarketplace.ViewComponents
{
    public class PendingRequestsViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public PendingRequestsViewComponent(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Do not show notifications to guests.
            if (User.Identity?.IsAuthenticated != true)
            {
                return Content(string.Empty);
            }

            // Get the currently logged-in user.
            var userId = HttpContext.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Content(string.Empty);
            }

            // Count unanswered requests for this owner.
            var pendingCount = await _context.PropertyInquiries
                .AsNoTracking()
                .CountAsync(i =>
                    i.OwnerId == userId &&
                    i.Status == PropertyInquiryStatus.Pending);

            // Render the notification link.
            return View(pendingCount);
        }
    }
}
