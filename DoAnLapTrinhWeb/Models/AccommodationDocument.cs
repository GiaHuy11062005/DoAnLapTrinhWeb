namespace DoAnLapTrinhWeb.Models
{
    public class AccommodationDocument
    {
        public int Id { get; set; }
        public string? OriginalFileName { get; set; }
        public string? StoredFileName { get; set; }
        public string? ContentType { get; set; }
        public long FileSize { get; set; }
        public int AccommodationId { get; set; }
        public Accommodation? Accommodation { get; set; }
    }
}
