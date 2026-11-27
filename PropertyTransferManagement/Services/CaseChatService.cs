using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Services
{
    public record ConversationSummary(int Id, CaseConversationKind Kind, string Title, int Unread);

    public record ChatMessageDto(long Id, int ConversationId, string SenderId, string SenderName,
        string SenderRole, string Content, DateTime SentAt);

    /// <summary>
    /// Keeps each case's three conversations in line with the case:
    /// buyers + officer, sellers + officer, and everyone (shared).
    /// Only active participants may read or write a conversation.
    /// </summary>
    public class CaseChatService
    {
        private readonly ApplicationDbContext _db;

        public CaseChatService(ApplicationDbContext db) => _db = db;

        public static string TitleFor(CaseConversationKind kind) => kind switch
        {
            CaseConversationKind.BuyerOfficer => "Buyer ↔ officer (private)",
            CaseConversationKind.SellerOfficer => "Seller ↔ officer (private)",
            _ => "Shared case discussion"
        };

        /// <summary>Creates missing conversations and syncs participants. Call SaveChanges afterwards.</summary>
        public async Task EnsureConversationsAsync(Guid caseId)
        {
            var transferCase = await _db.TransferCases.AsNoTracking().FirstAsync(c => c.Id == caseId);

            var parties = await _db.CaseParticipants
                .Where(p => p.TransferCaseId == caseId && p.IsActive)
                .Select(p => new { p.UserId, p.Role })
                .ToListAsync();

            var conversations = await _db.CaseConversations
                .Include(c => c.Participants)
                .Where(c => c.TransferCaseId == caseId)
                .ToListAsync();

            foreach (var kind in Enum.GetValues<CaseConversationKind>())
            {
                var conversation = conversations.FirstOrDefault(c => c.Kind == kind);
                if (conversation == null)
                {
                    conversation = new CaseConversation
                    {
                        TransferCaseId = caseId,
                        Kind = kind,
                        Title = TitleFor(kind),
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.CaseConversations.Add(conversation);
                }

                // Who should be in this conversation now
                var wanted = new Dictionary<string, CaseConversationRole>();
                foreach (var p in parties)
                {
                    bool include = kind switch
                    {
                        CaseConversationKind.BuyerOfficer => p.Role == CaseParticipantRole.Buyer,
                        CaseConversationKind.SellerOfficer => p.Role == CaseParticipantRole.Seller,
                        _ => true
                    };
                    if (include)
                        wanted[p.UserId] = p.Role == CaseParticipantRole.Buyer ? CaseConversationRole.Buyer : CaseConversationRole.Seller;
                }
                if (transferCase.AssignedOfficerId != null)
                    wanted[transferCase.AssignedOfficerId] = CaseConversationRole.Officer;

                foreach (var member in conversation.Participants)
                {
                    bool shouldBeActive = wanted.ContainsKey(member.UserId);
                    if (member.IsActive != shouldBeActive) member.IsActive = shouldBeActive;
                }

                foreach (var (userId, role) in wanted)
                {
                    if (conversation.Participants.All(m => m.UserId != userId))
                    {
                        conversation.Participants.Add(new CaseConversationParticipant
                        {
                            UserId = userId,
                            Role = role,
                            JoinedAt = DateTime.UtcNow,
                            IsActive = true
                        });
                    }
                }
            }
        }

        public async Task<List<ConversationSummary>> MyConversationsAsync(Guid caseId, string userId)
        {
            var rows = await _db.CaseConversationParticipants
                .Where(p => p.UserId == userId && p.IsActive && p.CaseConversation.TransferCaseId == caseId)
                .Select(p => new
                {
                    p.CaseConversation.Id,
                    p.CaseConversation.Kind,
                    p.CaseConversation.Title,
                    Unread = p.CaseConversation.Messages.Count(m =>
                        !m.IsDeleted && m.SenderId != userId && (p.LastReadAt == null || m.SentAt > p.LastReadAt))
                })
                .ToListAsync();

            return rows.OrderBy(r => r.Kind).Select(r => new ConversationSummary(r.Id, r.Kind, r.Title, r.Unread)).ToList();
        }

        public Task<CaseConversationParticipant?> MembershipAsync(int conversationId, string userId) =>
            _db.CaseConversationParticipants
                .Include(p => p.CaseConversation).ThenInclude(c => c.TransferCase)
                .FirstOrDefaultAsync(p => p.CaseConversationId == conversationId && p.UserId == userId && p.IsActive);

        public async Task<List<ChatMessageDto>> HistoryAsync(int conversationId, int take = 100)
        {
            var messages = await _db.CaseChatMessages
                .AsNoTracking()
                .Where(m => m.CaseConversationId == conversationId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .Take(take)
                .Select(m => new
                {
                    m.Id, m.CaseConversationId, m.SenderId,
                    Name = m.Sender.FirstName + " " + m.Sender.LastName,
                    Role = m.CaseConversation.Participants.Where(p => p.UserId == m.SenderId).Select(p => p.Role).FirstOrDefault(),
                    m.Content, m.SentAt
                })
                .ToListAsync();

            return messages.OrderBy(m => m.SentAt)
                .Select(m => new ChatMessageDto(m.Id, m.CaseConversationId, m.SenderId, m.Name, m.Role.ToString(), m.Content, m.SentAt))
                .ToList();
        }
    }
}
