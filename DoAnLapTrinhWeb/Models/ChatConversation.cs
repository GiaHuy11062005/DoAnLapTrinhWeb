namespace DoAnLapTrinhWeb.Models
{
    public class ChatConversation
    {
        public int Id { get; set; }

        public int AccommodationId { get; set; }
        public Accommodation? Accommodation { get; set; }

        public int RenterId { get; set; }
        public AppUser? Renter { get; set; }

        public int LandlordId { get; set; }
        public AppUser? Landlord { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ICollection<ChatMessage>? Messages { get; set; }
    }
}
