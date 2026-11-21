
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ==========================================
        // PROPERTY AND LISTINGS
        // ==========================================

        public DbSet<Property> Properties
            => Set<Property>();

        public DbSet<PropertyListing> PropertyListings
            => Set<PropertyListing>();

        public DbSet<PropertyPhoto> PropertyPhotos
            => Set<PropertyPhoto>();

        public DbSet<Favorite> Favorites
            => Set<Favorite>();

        public DbSet<SavedSearch> SavedSearches
            => Set<SavedSearch>();

        // ==========================================
        // CHAT
        // ==========================================

        public DbSet<Conversation> Conversations
            => Set<Conversation>();

        public DbSet<ConversationParticipant> ConversationParticipants
            => Set<ConversationParticipant>();

        public DbSet<ChatMessage> ChatMessages
            => Set<ChatMessage>();

        // ==========================================
        // TRANSACTIONS, INQUIRIES AND RENTALS
        // ==========================================

        public DbSet<Transaction> Transactions
            => Set<Transaction>();

        public DbSet<PropertyInquiry> PropertyInquiries
            => Set<PropertyInquiry>();

        public DbSet<RentalAgreement> RentalAgreements
            => Set<RentalAgreement>();

        public DbSet<PropertyReview> PropertyReviews
            => Set<PropertyReview>();

        public DbSet<TransferRequest> TransferRequests
            => Set<TransferRequest>();

        // ==========================================
        // NOTIFICATIONS AND MODERATION
        // ==========================================

        public DbSet<Notification> Notifications
            => Set<Notification>();

        public DbSet<PropertyReport> PropertyReports
            => Set<PropertyReport>();

        public DbSet<ListingModeration> ListingModerations
            => Set<ListingModeration>();

        public DbSet<AuditLog> AuditLogs
            => Set<AuditLog>();

        // ==========================================
        // PROMOTIONS AND PAYMENTS
        // ==========================================

        public DbSet<PromotionPlan> PromotionPlans
            => Set<PromotionPlan>();

        public DbSet<ListingPromotion> ListingPromotions
            => Set<ListingPromotion>();

        public DbSet<Payment> Payments
            => Set<Payment>();

        // ==========================================
        // USER VERIFICATION
        // ==========================================

        public DbSet<UserVerification> UserVerifications
            => Set<UserVerification>();

        public DbSet<UserDocument> UserDocuments
            => Set<UserDocument>();

        // ==========================================
        // RABBITMQ RELIABILITY
        // ==========================================

        public DbSet<OutboxMessage> OutboxMessages
            => Set<OutboxMessage>();

        public DbSet<InboxMessage> InboxMessages
            => Set<InboxMessage>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Preserve the existing SQL Server table name.
            builder.Entity<Transaction>()
                .ToTable("Transaction");

            // ==========================================
            // PROPERTY INQUIRY RELATIONSHIPS
            // ==========================================

            builder.Entity<PropertyInquiry>()
                .HasOne(i => i.PropertyListing)
                .WithMany()
                .HasForeignKey(i => i.PropertyListingId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<PropertyInquiry>()
                .HasOne(i => i.Requester)
                .WithMany()
                .HasForeignKey(i => i.RequesterId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<PropertyInquiry>()
                .HasOne(i => i.Owner)
                .WithMany()
                .HasForeignKey(i => i.OwnerId)
                .OnDelete(DeleteBehavior.NoAction);

            // ==========================================
            // TRANSACTION RELATIONSHIPS
            // ==========================================

            // Link each transaction to its accepted inquiry.
            builder.Entity<Transaction>()
                .HasOne(t => t.PropertyInquiry)
                .WithMany()
                .HasForeignKey(t => t.PropertyInquiryId)
                .OnDelete(DeleteBehavior.NoAction);

            // One transaction per property inquiry.
            builder.Entity<Transaction>()
                .HasIndex(t => t.PropertyInquiryId)
                .IsUnique()
                .HasFilter("[PropertyInquiryId] IS NOT NULL");

            // ==========================================
            // PREVENT MULTIPLE ONGOING TRANSACTIONS
            // ==========================================

            // Allow historical transactions for the same listing.
            //
            // Only one transaction can have one of these
            // ongoing statuses for the same property:
            //
            // 1 = PendingConfirmation
            // 2 = Confirmed
            // 3 = TransferRequested
            // 4 = TransferInProgress
            //
            // Completed, Cancelled and Rejected transactions
            // are excluded from the unique index.

            builder.Entity<Transaction>()
                .HasIndex(t => t.PropertyListingId)
                .IsUnique()
                .HasFilter("[Status] IN (1, 2, 3, 4)");


            // ==========================================
            // RENTAL AGREEMENT RELATIONSHIPS
            // ==========================================

            builder.Entity<RentalAgreement>()
                .ToTable("RentalAgreements");

            // One transaction can have multiple proposals.
            // This preserves rejected proposal history.
            builder.Entity<RentalAgreement>()
                .HasOne(r => r.Transaction)
                .WithMany()
                .HasForeignKey(r => r.TransactionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Only one ongoing agreement is allowed
            // per transaction.
            //
            // 1 = Draft
            // 2 = PendingApproval
            // 3 = Scheduled
            // 4 = Active
            //
            // Rejected, Cancelled, Expired and Completed
            // agreements remain in the database as history.

            builder.Entity<RentalAgreement>()
                .HasIndex(r => r.TransactionId)
                .IsUnique()
                .HasFilter("[Status] IN (1, 2, 3, 4)");

            // Store rent with two decimal places.
            builder.Entity<RentalAgreement>()
                .Property(r => r.MonthlyRent)
                .HasPrecision(18, 2);


            // ==========================================
            // TRANSFER REQUEST RELATIONSHIP
            // ==========================================

            builder.Entity<TransferRequest>()
                .HasOne(tr => tr.Transaction)
                .WithMany()
                .HasForeignKey(tr => tr.TransactionId)
                .OnDelete(DeleteBehavior.NoAction);

            // ==========================================
            // PREVENT CASCADE DELETE
            // ==========================================

            foreach (var foreignKey in builder.Model
                .GetEntityTypes()
                .SelectMany(entity => entity.GetForeignKeys()))
            {
                if (!foreignKey.IsOwnership)
                {
                    foreignKey.DeleteBehavior =
                        DeleteBehavior.NoAction;
                }
            }

            // ==========================================
            // UNIQUE INDEXES
            // ==========================================

            // Unique favorites per user and listing.
            builder.Entity<Favorite>()
                .HasIndex(f => new
                {
                    f.UserId,
                    f.PropertyListingId
                })
                .IsUnique();

            // Unique conversation participants.
            builder.Entity<ConversationParticipant>()
                .HasIndex(p => new
                {
                    p.ConversationId,
                    p.UserId
                })
                .IsUnique();

            // One review per transaction.
            builder.Entity<PropertyReview>()
                .HasIndex(r => r.TransactionId)
                .IsUnique();

            // Prevent duplicate RabbitMQ events.
            builder.Entity<InboxMessage>()
                .HasIndex(m => m.MessageId)
                .IsUnique();

            // Prevent duplicate payment requests.
            builder.Entity<Payment>()
                .HasIndex(p => p.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            // Unique payment provider transaction reference.
            builder.Entity<Payment>()
                .HasIndex(p => p.ProviderTransactionId)
                .IsUnique()
                .HasFilter("[ProviderTransactionId] IS NOT NULL");

            // ==========================================
            // MONEY PRECISION
            // ==========================================

            builder.Entity<PropertyListing>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            builder.Entity<Transaction>()
                .Property(t => t.AgreedPrice)
                .HasPrecision(18, 2);

            builder.Entity<PromotionPlan>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            builder.Entity<ListingPromotion>()
                .Property(p => p.AmountPaid)
                .HasPrecision(18, 2);

            builder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            // ==========================================
            // PROPERTY MEASUREMENTS
            // ==========================================

            builder.Entity<Property>()
                .Property(p => p.SizeSquareMeters)
                .HasPrecision(18, 2);

            builder.Entity<Property>()
                .Property(p => p.Latitude)
                .HasPrecision(10, 7);

            builder.Entity<Property>()
                .Property(p => p.Longitude)
                .HasPrecision(10, 7);

            // ==========================================
            // SAVED SEARCH PRECISION
            // ==========================================

            builder.Entity<SavedSearch>()
                .Property(s => s.MinPrice)
                .HasPrecision(18, 2);

            builder.Entity<SavedSearch>()
                .Property(s => s.MaxPrice)
                .HasPrecision(18, 2);

            builder.Entity<SavedSearch>()
                .Property(s => s.MinSizeSquareMeters)
                .HasPrecision(18, 2);

            builder.Entity<SavedSearch>()
                .Property(s => s.MaxSizeSquareMeters)
                .HasPrecision(18, 2);
        }
    }
}
