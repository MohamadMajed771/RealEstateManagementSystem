namespace SharedContracts
{
    public class ListingApprovedEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();

        public int PropertyListingId { get; set; }

        public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
    }
}