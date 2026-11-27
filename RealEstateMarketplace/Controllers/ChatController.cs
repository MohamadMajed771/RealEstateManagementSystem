
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // MY CONVERSATIONS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var conversations = await _context.Conversations
                .AsNoTracking()
                .Include(c => c.PropertyListing)
                .Include(c => c.Participants)
                .Where(c => c.Participants.Any(p =>
                    p.UserId == userId && p.IsActive))
                .OrderByDescending(c =>
                    c.LastMessageAt ?? c.CreatedAt)
                .ToListAsync();

            return View(conversations);
        }

        // ==========================================
        // START A CONVERSATION
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int propertyListingId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var listing = await _context.PropertyListings
                .Include(l => l.Property)
                .FirstOrDefaultAsync(l =>
                    l.Id == propertyListingId &&
                    // Same rule as the public listing page: hidden listings cannot be discussed.
                    (l.Status == ListingStatus.Approved ||
                     l.Status == ListingStatus.Sold ||
                     l.Status == ListingStatus.Rented) &&
                    !l.IsDeleted);

            if (listing == null)
                return NotFound();

            string landlordId = listing.Property.OwnerId;

            if (landlordId == userId)
            {
                TempData["Error"] =
                    "You cannot start a conversation with yourself.";

                return RedirectToAction(
                    "Details",
                    "Properties",
                    new { id = listing.Id });
            }

            // Reuse an existing conversation between
            // the same two users about this listing.
            var existingConversation = await _context.Conversations
                .Where(c =>
                    c.PropertyListingId == propertyListingId &&
                    c.Participants.Any(p =>
                        p.UserId == userId && p.IsActive) &&
                    c.Participants.Any(p =>
                        p.UserId == landlordId && p.IsActive))
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingConversation != null)
            {
                if (existingConversation.IsClosed)
                {
                    TempData["Error"] =
                        "This conversation is closed.";

                    return RedirectToAction(nameof(Index));
                }

                return RedirectToAction(
                    nameof(Details),
                    new { id = existingConversation.Id });
            }

            var conversation = new Conversation
            {
                PropertyListingId = propertyListingId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                IsClosed = false
            };

            conversation.Participants.Add(
                new ConversationParticipant
                {
                    UserId = userId,
                    Role = ConversationParticipantRole.Buyer,
                    IsActive = true
                });

            conversation.Participants.Add(
                new ConversationParticipant
                {
                    UserId = landlordId,
                    Role = ConversationParticipantRole.Seller,
                    IsActive = true
                });

            _context.Conversations.Add(conversation);

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = conversation.Id });
        }

        // ==========================================
        // CONVERSATION DETAILS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var conversation = await _context.Conversations
                .AsNoTracking()
                .Include(c => c.PropertyListing)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.Participants.Any(p =>
                        p.UserId == userId &&
                        p.IsActive));

            if (conversation == null)
                return NotFound();

            // Only show messages that have not
            // been soft-deleted.
            conversation.Messages = conversation.Messages
                .Where(m => !m.IsDeleted)
                .OrderBy(m => m.SentAt)
                .ToList();

            ViewBag.CurrentUserId = userId;

            return View(conversation);
        }
    }
}
