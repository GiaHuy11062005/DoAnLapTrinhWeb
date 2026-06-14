namespace DoAnLapTrinhWeb.Models
{
    public class RentalRequest
    {
        public int Id { get; set; }
        public int AccommodationId { get; set; }
        public int RenterId { get; set; }
        public int Occupants { get; set; } = 1;
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ConfirmedAt { get; set; }
        public Accommodation? Accommodation { get; set; }
        public AppUser? Renter { get; set; }
    }
}
