using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services;

namespace PropertyTransferManagement.Hubs
{
    /// <summary>
    /// Real-time case chat. Every call checks that the caller is an
    /// active participant of the conversation.
    /// </summary>
    [Authorize]
    public class CaseChatHub : Hub
    {
        public const int MaxLength = 3000;

        private readonly ApplicationDbContext _db;
        private readonly CaseChatService _chat;
        private readonly CaseService _cases;

        public CaseChatHub(ApplicationDbContext db, CaseChatService chat, CaseService cases)
        {
            _db = db;
            _chat = chat;
            _cases = cases;
        }

        private string UserId => Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;

        private static string Group(int conversationId) => "case-conversation-" + conversationId;

        /// <summary>Joins the live group and returns the latest messages.</summary>
        public async Task<List<ChatMessageDto>> Join(int conversationId)
        {
            var membership = await _chat.MembershipAsync(conversationId, UserId)
                ?? throw new HubException("You are not part of this conversation.");

            await Groups.AddToGroupAsync(Context.ConnectionId, Group(conversationId));

            membership.LastReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return await _chat.HistoryAsync(conversationId);
        }

        public async Task Leave(int conversationId) =>
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(conversationId));

        public async Task Send(int conversationId, string content)
        {
            content = (content ?? "").Trim();
            if (content.Length == 0) return;
            if (content.Length > MaxLength) throw new HubException($"Messages are limited to {MaxLength} characters.");

            var membership = await _chat.MembershipAsync(conversationId, UserId)
                ?? throw new HubException("You are not part of this conversation.");

            var conversation = membership.CaseConversation;
            if (conversation.IsClosed) throw new HubException("This conversation is closed.");

            var now = DateTime.UtcNow;
            var previousMessageAt = conversation.LastMessageAt;

            var message = new CaseChatMessage
            {
                CaseConversationId = conversationId,
                SenderId = UserId,
                Content = content,
                SentAt = now
            };
            _db.CaseChatMessages.Add(message);
            conversation.LastMessageAt = now;
            membership.LastReadAt = now;

            // One in-app notification per burst: only recipients who had
            // read everything before this message get a new notification.
            var others = _db.CaseConversationParticipants
                .Where(p => p.CaseConversationId == conversationId && p.IsActive && p.UserId != UserId)
                .ToList();

            var sender = await _db.Users.FindAsync(UserId);
            var senderName = sender == null ? "Someone" : $"{sender.FirstName} {sender.LastName}";
            var caseInfo = conversation.TransferCase;

            foreach (var p in others)
            {
                bool hadUnread = previousMessageAt != null && (p.LastReadAt == null || p.LastReadAt < previousMessageAt);
                if (!hadUnread)
                {
                    _cases.Notify(p.UserId, caseInfo.Id,
                        $"New message on case {caseInfo.CaseNumber}",
                        $"{senderName} ({conversation.Title}): {(content.Length > 140 ? content[..140] + "…" : content)}",
                        CaseNotificationType.NewMessage);
                }
            }

            await _db.SaveChangesAsync();

            await Clients.Group(Group(conversationId)).SendAsync("message",
                new ChatMessageDto(message.Id, conversationId, UserId, senderName, membership.Role.ToString(), content, now));
        }

        public async Task MarkRead(int conversationId)
        {
            var membership = await _chat.MembershipAsync(conversationId, UserId);
            if (membership == null) return;
            membership.LastReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
