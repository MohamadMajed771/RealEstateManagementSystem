
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

        // Property and listings
        public DbSet<Property> Properties => Set<Property>();
        public DbSet<PropertyListing> PropertyListings => Set<PropertyListing>();
        public DbSet<PropertyPhoto> PropertyPhotos => Set<PropertyPhoto>();
        public DbSet<Favorite> Favorites => Set<Favorite>();
        public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();

        // Chat
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ConversationParticipant> ConversationParticipants
            => Set<ConversationParticipant>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

        // Transactions, inquiries and reviews
        public DbSet<PropertyInquiry> PropertyInquiries => Set<PropertyInquiry>();
        public DbSet<PropertyReview> PropertyReviews => Set<PropertyReview>();
        public DbSet<TransferRequest> TransferRequests => Set<TransferRequest>();

        // Notifications and moderation
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<PropertyReport> PropertyReports => Set<PropertyReport>();
        public DbSet<ListingModeration> ListingModerations
            => Set<ListingModeration>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        // Promotions and payments
        public DbSet<PromotionPlan> PromotionPlans => Set<PromotionPlan>();
        public DbSet<ListingPromotion> ListingPromotions
            => Set<ListingPromotion>();
        public DbSet<Payment> Payments => Set<Payment>();

        // User verification
        public DbSet<UserVerification> UserVerifications
            => Set<UserVerification>();
        public DbSet<UserDocument> UserDocuments => Set<UserDocument>();

        // RabbitMQ reliability
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Property inquiry relationships
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

            // Explicit relationship:
            // One Transaction can have multiple TransferRequests.
            builder.Entity<TransferRequest>()
                .HasOne(tr => tr.Transaction)
                .WithMany()
                .HasForeignKey(tr => tr.TransactionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Prevent cascading deletion through marketplace relationships.
            // This avoids multiple cascade paths in SQL Server.
            foreach (var foreignKey in builder.Model
                .GetEntityTypes()
                .SelectMany(entity => entity.GetForeignKeys()))
            {
                if (!foreignKey.IsOwnership)
                {
                    foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
                }
            }

            // Unique favorites per user and listing
            builder.Entity<Favorite>()
                .HasIndex(f => new
                {
                    f.UserId,
                    f.PropertyListingId
                })
                .IsUnique();

            // Unique conversation participants
            builder.Entity<ConversationParticipant>()
                .HasIndex(p => new
                {
                    p.ConversationId,
                    p.UserId
                })
                .IsUnique();

            // One review per transaction
            builder.Entity<PropertyReview>()
                .HasIndex(r => r.TransactionId)
                .IsUnique();

            // Prevent duplicate incoming RabbitMQ events
            builder.Entity<InboxMessage>()
                .HasIndex(m => m.MessageId)
                .IsUnique();

            // Prevent duplicate payment requests
            builder.Entity<Payment>()
                .HasIndex(p => p.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            // Payment provider transaction reference
            builder.Entity<Payment>()
                .HasIndex(p => p.ProviderTransactionId)
                .IsUnique()
                .HasFilter("[ProviderTransactionId] IS NOT NULL");

            // Money precision
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

            // Property measurements and coordinates
            builder.Entity<Property>()
                .Property(p => p.SizeSquareMeters)
                .HasPrecision(18, 2);

            builder.Entity<Property>()
                .Property(p => p.Latitude)
                .HasPrecision(10, 7);

            builder.Entity<Property>()
                .Property(p => p.Longitude)
                .HasPrecision(10, 7);

            // Saved search decimal values
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
