
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _context.Users.CountAsync();

            ViewBag.TotalListings = await _context.PropertyListings
                .CountAsync(l => !l.IsDeleted);

            ViewBag.PendingListings = await _context.PropertyListings
                .CountAsync(l =>
                    !l.IsDeleted &&
                    l.Status == ListingStatus.PendingApproval);

            ViewBag.ApprovedListings = await _context.PropertyListings
                .CountAsync(l =>
                    !l.IsDeleted &&
                    l.Status == ListingStatus.Approved);

            ViewBag.RejectedListings = await _context.PropertyListings
                .CountAsync(l =>
                    !l.IsDeleted &&
                    l.Status == ListingStatus.Rejected);

            ViewBag.TotalReports = await _context.PropertyReports
                .CountAsync();

            return View();
        }
    }
}
