namespace DoAnLapTrinhWeb.Models
{
    public class AppNotification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Url { get; set; }
        public string Type { get; set; } = "General";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public AppUser? User { get; set; }
    }
}
