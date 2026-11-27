
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyTransferManagement.Models;

namespace PropertyTransferManagement.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Transfer case management
        public DbSet<TransferCase> TransferCases => Set<TransferCase>();
        public DbSet<CaseParticipant> CaseParticipants => Set<CaseParticipant>();
        public DbSet<CaseInvitation> CaseInvitations => Set<CaseInvitation>();
        public DbSet<CaseStatusHistory> CaseStatusHistories => Set<CaseStatusHistory>();

        // Documents and verification
        public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
        public DbSet<CaseDocumentRequirement> CaseDocumentRequirements
            => Set<CaseDocumentRequirement>();
        public DbSet<CaseDocument> CaseDocuments => Set<CaseDocument>();
        public DbSet<DocumentReview> DocumentReviews => Set<DocumentReview>();

        // Officers and appointments
        public DbSet<OfficerAssignment> OfficerAssignments => Set<OfficerAssignment>();
        public DbSet<CaseAppointment> CaseAppointments => Set<CaseAppointment>();

        // Private chat
        public DbSet<CaseConversation> CaseConversations => Set<CaseConversation>();
        public DbSet<CaseConversationParticipant> CaseConversationParticipants
            => Set<CaseConversationParticipant>();
        public DbSet<CaseChatMessage> CaseChatMessages => Set<CaseChatMessage>();

        // Notifications and official confirmations
        public DbSet<CaseNotification> CaseNotifications => Set<CaseNotification>();
        public DbSet<OfficialConfirmation> OfficialConfirmations
            => Set<OfficialConfirmation>();

        // Financial assistance and payments
        public DbSet<LoanAssistanceRequest> LoanAssistanceRequests
            => Set<LoanAssistanceRequest>();
        public DbSet<ServicePayment> ServicePayments => Set<ServicePayment>();

        // Auditing
        public DbSet<CaseAuditLog> CaseAuditLogs => Set<CaseAuditLog>();

        // RabbitMQ reliability
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Explicit relationship for transfer cases and participants
            builder.Entity<CaseParticipant>()
                .HasOne(p => p.TransferCase)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.TransferCaseId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<CaseInvitation>()
                .HasOne(i => i.TransferCase)
                .WithMany(c => c.Invitations)
                .HasForeignKey(i => i.TransferCaseId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<CaseDocument>()
                .HasOne(d => d.TransferCase)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.TransferCaseId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<CaseStatusHistory>()
                .HasOne(h => h.TransferCase)
                .WithMany(c => c.StatusHistory)
                .HasForeignKey(h => h.TransferCaseId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<OfficerAssignment>()
                .HasOne(a => a.TransferCase)
                .WithMany(c => c.OfficerAssignments)
                .HasForeignKey(a => a.TransferCaseId)
                .OnDelete(DeleteBehavior.NoAction);

            // Prevent SQL Server multiple cascade path errors
            foreach (var foreignKey in builder.Model
                .GetEntityTypes()
                .SelectMany(entity => entity.GetForeignKeys()))
            {
                if (!foreignKey.IsOwnership)
                {
                    foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
                }
            }

            // Unique human-readable case number
            builder.Entity<TransferCase>()
                .HasIndex(c => c.CaseNumber)
                .IsUnique();

            builder.Entity<TransferCase>()
                .Property(c => c.SizeSquareMeters)
                .HasPrecision(18, 2);

            // Current officer of the case
            builder.Entity<TransferCase>()
                .HasOne(c => c.AssignedOfficer)
                .WithMany()
                .HasForeignKey(c => c.AssignedOfficerId)
                .OnDelete(DeleteBehavior.NoAction);

            // One transfer case per marketplace transfer request
            builder.Entity<TransferCase>()
                .HasIndex(c => c.MarketplaceTransferRequestId)
                .IsUnique();

            // Prevent duplicate case participants
            builder.Entity<CaseParticipant>()
                .HasIndex(p => new
                {
                    p.TransferCaseId,
                    p.UserId,
                    p.Role
                })
                .IsUnique();

            // Prevent duplicate conversation membership
            builder.Entity<CaseConversationParticipant>()
                .HasIndex(p => new
                {
                    p.CaseConversationId,
                    p.UserId
                })
                .IsUnique();

            // Unique document type code
            builder.Entity<DocumentType>()
                .HasIndex(d => d.Code)
                .IsUnique();

            // Unique invitation token hashes
            builder.Entity<CaseInvitation>()
                .HasIndex(i => i.TokenHash)
                .IsUnique();

            // Prevent duplicate RabbitMQ message processing
            builder.Entity<InboxMessage>()
                .HasIndex(m => m.MessageId)
                .IsUnique();

            // Payment idempotency
            builder.Entity<ServicePayment>()
                .HasIndex(p => p.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            // Payment provider transaction reference
            builder.Entity<ServicePayment>()
                .HasIndex(p => p.ProviderTransactionId)
                .IsUnique()
                .HasFilter("[ProviderTransactionId] IS NOT NULL");

            // Decimal precision
            builder.Entity<TransferCase>()
                .Property(c => c.AgreedPrice)
                .HasPrecision(18, 2);

            builder.Entity<LoanAssistanceRequest>()
                .Property(r => r.RequestedAmount)
                .HasPrecision(18, 2);

            builder.Entity<ServicePayment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);
        }
    }
}
