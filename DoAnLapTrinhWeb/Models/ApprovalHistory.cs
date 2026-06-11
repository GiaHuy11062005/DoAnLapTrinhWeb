using System;

namespace DoAnLapTrinhWeb.Models
{
    public class ApprovalHistory
    {
        public int Id { get; set; }
        public int? AccommodationId { get; set; }
        public string? AccommodationTitle { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerEmail { get; set; }
        public decimal Price { get; set; }
        public string? Action { get; set; }
        public string? ProcessedBy { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
