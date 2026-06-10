namespace DoAnLapTrinhWeb.Models
{
    // Model Hình ảnh
    public class AccommodationImage
    {
        public int Id { get; set; }
        public string? ImageUrl { get; set; }
        public int AccommodationId { get; set; }
        public Accommodation? Accommodation { get; set; }
    }
}