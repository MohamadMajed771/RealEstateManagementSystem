
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatHub(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // JOIN A PRIVATE CONVERSATION
        // ==========================================

        public async Task JoinConversation(int conversationId)
        {
            var userId = _userManager.GetUserId(Context.User!);

            if (string.IsNullOrEmpty(userId))
                throw new HubException("Authentication required.");

            bool isParticipant =
                await _context.ConversationParticipants
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.ConversationId == conversationId &&
                        p.UserId == userId &&
                        p.IsActive);

            if (!isParticipant)
                throw new HubException(
                    "You are not authorized to join this conversation.");

            bool conversationOpen =
                await _context.Conversations
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.Id == conversationId &&
                        !c.IsClosed);

            if (!conversationOpen)
                throw new HubException(
                    "This conversation is closed or unavailable.");

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"conversation-{conversationId}");
        }

        // ==========================================
        // LEAVE CONVERSATION
        // ==========================================

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"conversation-{conversationId}");
        }

        // ==========================================
        // SEND MESSAGE
        // ==========================================

        public async Task SendMessage(
            int conversationId,
            string content)
        {
            var userId = _userManager.GetUserId(Context.User!);

            if (string.IsNullOrEmpty(userId))
                throw new HubException("Authentication required.");

            content = content?.Trim() ?? string.Empty;

            if (content.Length < 1 || content.Length > 2000)
                throw new HubException(
                    "Message must contain between 1 and 2000 characters.");

            // Verify permission on every message.
            bool isParticipant =
                await _context.ConversationParticipants
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.ConversationId == conversationId &&
                        p.UserId == userId &&
                        p.IsActive);

            if (!isParticipant)
                throw new HubException(
                    "You are not authorized to send messages here.");

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null || conversation.IsClosed)
                throw new HubException(
                    "This conversation is closed or unavailable.");

            var now = DateTime.UtcNow;

            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderId = userId,
                Content = content,
                SentAt = now,
                IsDeleted = false
            };

            _context.ChatMessages.Add(message);

            conversation.LastMessageAt = now;

            await _context.SaveChangesAsync();

            // Notify only currently active participants.
            var participantIds = await _context.ConversationParticipants
                .AsNoTracking()
                .Where(p =>
                    p.ConversationId == conversationId &&
                    p.IsActive)
                .Select(p => p.UserId)
                .ToListAsync();

            await Clients.Users(participantIds)
                .SendAsync(
                    "ReceiveMessage",
                    new
                    {
                        id = message.Id,
                        conversationId,
                        senderId = userId,
                        content = message.Content,
                        sentAt = message.SentAt
                    });
        }
    }
}
