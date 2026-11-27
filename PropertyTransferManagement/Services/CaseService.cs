using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Data;
using PropertyTransferManagement.Models;
using PropertyTransferManagement.Services.Messaging;
using SharedContracts.Events;

namespace PropertyTransferManagement.Services
{
    /// <summary>
    /// Allowed manual status changes by an officer.
    /// OfficialCompletionConfirmed is NOT in this list: it can only be
    /// reached by recording an OfficialConfirmation (authority + reference).
    /// </summary>
    public static class CaseWorkflow
    {
        private static readonly Dictionary<TransferCaseStatus, TransferCaseStatus[]> Allowed = new()
        {
            [TransferCaseStatus.RequestReceived] = new[]
            {
                TransferCaseStatus.AwaitingDocuments, TransferCaseStatus.OnHold,
                TransferCaseStatus.Rejected, TransferCaseStatus.Cancelled
            },
            [TransferCaseStatus.AwaitingDocuments] = new[]
            {
                TransferCaseStatus.DocumentsUnderReview, TransferCaseStatus.OnHold,
                TransferCaseStatus.Rejected, TransferCaseStatus.Cancelled
            },
            [TransferCaseStatus.DocumentsUnderReview] = new[]
            {
                TransferCaseStatus.AdditionalInformationRequired, TransferCaseStatus.ReadyForProcessing,
                TransferCaseStatus.OnHold, TransferCaseStatus.Rejected
            },
            [TransferCaseStatus.AdditionalInformationRequired] = new[]
            {
                TransferCaseStatus.DocumentsUnderReview, TransferCaseStatus.OnHold,
                TransferCaseStatus.Rejected, TransferCaseStatus.Cancelled
            },
            [TransferCaseStatus.ReadyForProcessing] = new[]
            {
                TransferCaseStatus.ExternalProcessingInProgress, TransferCaseStatus.OnHold,
                TransferCaseStatus.Rejected
            },
            [TransferCaseStatus.ExternalProcessingInProgress] = new[]
            {
                TransferCaseStatus.OnHold, TransferCaseStatus.Rejected
            },
            [TransferCaseStatus.OfficialCompletionConfirmed] = new[]
            {
                TransferCaseStatus.CaseClosed
            },
            [TransferCaseStatus.OnHold] = new[]
            {
                TransferCaseStatus.AwaitingDocuments, TransferCaseStatus.DocumentsUnderReview,
                TransferCaseStatus.ReadyForProcessing, TransferCaseStatus.ExternalProcessingInProgress,
                TransferCaseStatus.Rejected, TransferCaseStatus.Cancelled
            },
            [TransferCaseStatus.Rejected] = new[] { TransferCaseStatus.CaseClosed },
            [TransferCaseStatus.Cancelled] = new[] { TransferCaseStatus.CaseClosed },
            [TransferCaseStatus.CaseClosed] = Array.Empty<TransferCaseStatus>()
        };

        public static IReadOnlyList<TransferCaseStatus> NextStatuses(TransferCaseStatus current) =>
            Allowed.TryGetValue(current, out var next) ? next : Array.Empty<TransferCaseStatus>();

        public static bool IsFinished(TransferCaseStatus status) =>
            status is TransferCaseStatus.CaseClosed or TransferCaseStatus.Rejected
                or TransferCaseStatus.Cancelled or TransferCaseStatus.OfficialCompletionConfirmed;

        public static string Display(TransferCaseStatus status) =>
            TransferStatusText.Humanize(status.ToString());

        public static string BadgeClass(TransferCaseStatus status) => status switch
        {
            TransferCaseStatus.RequestReceived => "bg-secondary",
            TransferCaseStatus.AwaitingDocuments => "bg-warning text-dark",
            TransferCaseStatus.DocumentsUnderReview => "bg-info text-dark",
            TransferCaseStatus.AdditionalInformationRequired => "bg-danger",
            TransferCaseStatus.ReadyForProcessing => "bg-primary",
            TransferCaseStatus.ExternalProcessingInProgress => "bg-primary",
            TransferCaseStatus.OfficialCompletionConfirmed => "bg-success",
            TransferCaseStatus.CaseClosed => "bg-dark",
            TransferCaseStatus.OnHold => "bg-warning text-dark",
            _ => "bg-danger"
        };
    }

    /// <summary>
    /// Shared case operations: status changes (with history, audit,
    /// notifications and RabbitMQ outbox event), notifications, audit log.
    /// Callers save changes with a single SaveChangesAsync so the business
    /// change and the outbox message are committed together.
    /// </summary>
    public class CaseService
    {
        private readonly ApplicationDbContext _db;

        public CaseService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task ChangeStatusAsync(
            TransferCase transferCase,
            TransferCaseStatus newStatus,
            string? changedByUserId,
            string? reason,
            bool publishStatusChanged = true)
        {
            var previous = transferCase.Status;
            if (previous == newStatus)
                return;

            var now = DateTime.UtcNow;

            transferCase.Status = newStatus;
            transferCase.UpdatedAt = now;

            if (newStatus is TransferCaseStatus.OfficialCompletionConfirmed or TransferCaseStatus.CaseClosed)
                transferCase.CompletedAt ??= now;

            _db.CaseStatusHistories.Add(new CaseStatusHistory
            {
                TransferCaseId = transferCase.Id,
                PreviousStatus = previous,
                NewStatus = newStatus,
                ChangedByUserId = changedByUserId,
                Reason = reason,
                ChangedAt = now
            });

            Audit(transferCase.Id, changedByUserId, "StatusChanged", "TransferCase",
                transferCase.Id.ToString(),
                $"{CaseWorkflow.Display(previous)} -> {CaseWorkflow.Display(newStatus)}" +
                (string.IsNullOrWhiteSpace(reason) ? "" : $". Reason: {reason}"));

            await NotifyParticipantsAsync(transferCase,
                $"Case {transferCase.CaseNumber}: {CaseWorkflow.Display(newStatus)}",
                $"Your case status changed to '{CaseWorkflow.Display(newStatus)}'." +
                (string.IsNullOrWhiteSpace(reason) ? "" : $" Note: {reason}"),
                CaseNotificationType.CaseStatusChanged);

            if (publishStatusChanged)
            {
                Outbox.Add(_db, new TransferStatusChanged
                {
                    TransferRequestId = transferCase.MarketplaceTransferRequestId,
                    CaseNumber = transferCase.CaseNumber,
                    PreviousStatus = previous.ToString(),
                    NewStatus = newStatus.ToString(),
                    Note = reason
                });
            }
        }

        public async Task NotifyParticipantsAsync(
            TransferCase transferCase,
            string title,
            string message,
            CaseNotificationType type,
            CaseParticipantRole? onlyRole = null)
        {
            var userIds = await _db.CaseParticipants
                .Where(p =>
                    p.TransferCaseId == transferCase.Id &&
                    p.IsActive &&
                    (onlyRole == null || p.Role == onlyRole))
                .Select(p => p.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var userId in userIds)
                Notify(userId, transferCase.Id, title, message, type);
        }

        public void Notify(
            string userId, Guid caseId, string title, string message, CaseNotificationType type)
        {
            _db.CaseNotifications.Add(new CaseNotification
            {
                UserId = userId,
                TransferCaseId = caseId,
                Title = title.Length > 200 ? title[..200] : title,
                Message = message.Length > 2000 ? message[..2000] : message,
                Type = type,
                ActionUrl = $"/Cases/Details/{caseId}",
                CreatedAt = DateTime.UtcNow
            });
        }

        // Never put document contents or personal ID data in audit text.
        public void Audit(
            Guid? caseId, string? userId, string action, string entityType,
            string? entityId, string description)
        {
            _db.CaseAuditLogs.Add(new CaseAuditLog
            {
                TransferCaseId = caseId,
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description.Length > 2000 ? description[..2000] : description,
                CreatedAt = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Links a signed-in user to every pending invitation sent to
        /// their confirmed email address (buyer / seller of a case).
        /// Returns how many cases were linked.
        /// </summary>
        public async Task<int> ClaimInvitationsAsync(ApplicationUser user)
        {
            if (!user.EmailConfirmed || string.IsNullOrEmpty(user.Email))
                return 0;

            var now = DateTime.UtcNow;
            var email = user.Email.ToUpperInvariant();

            var invitations = await _db.CaseInvitations
                .Include(i => i.TransferCase)
                .Where(i =>
                    i.Status == CaseInvitationStatus.Pending &&
                    i.ExpiresAt > now &&
                    i.Email.ToUpper() == email)
                .ToListAsync();

            foreach (var invitation in invitations)
            {
                bool alreadyLinked = await _db.CaseParticipants.AnyAsync(p =>
                    p.TransferCaseId == invitation.TransferCaseId &&
                    p.UserId == user.Id &&
                    p.Role == invitation.IntendedRole);

                if (!alreadyLinked)
                {
                    _db.CaseParticipants.Add(new CaseParticipant
                    {
                        TransferCaseId = invitation.TransferCaseId,
                        UserId = user.Id,
                        Role = invitation.IntendedRole,
                        JoinedAt = now
                    });
                }

                invitation.Status = CaseInvitationStatus.Accepted;
                invitation.AcceptedAt = now;
                invitation.AcceptedByUserId = user.Id;

                Audit(invitation.TransferCaseId, user.Id, "InvitationAccepted", "CaseInvitation",
                    invitation.Id.ToString(), $"{invitation.IntendedRole} joined the case.");

                if (invitation.TransferCase.AssignedOfficerId != null)
                {
                    Notify(invitation.TransferCase.AssignedOfficerId, invitation.TransferCaseId,
                        $"{invitation.IntendedRole} joined case {invitation.TransferCase.CaseNumber}",
                        $"The {invitation.IntendedRole.ToString().ToLower()} signed in and can now upload documents.",
                        CaseNotificationType.SystemAnnouncement);
                }

                // Fill the user's name from the marketplace data if empty.
                if (string.IsNullOrWhiteSpace(user.FirstName))
                {
                    var fullName = invitation.IntendedRole == CaseParticipantRole.Buyer
                        ? invitation.TransferCase.BuyerFullName
                        : invitation.TransferCase.SellerFullName;

                    var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    user.FirstName = parts.ElementAtOrDefault(0) ?? string.Empty;
                    user.LastName = parts.ElementAtOrDefault(1) ?? string.Empty;
                }
            }

            if (invitations.Count > 0)
                await _db.SaveChangesAsync();

            return invitations.Count;
        }
    }
}
