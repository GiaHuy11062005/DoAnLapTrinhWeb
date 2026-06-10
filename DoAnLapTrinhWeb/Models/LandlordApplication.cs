using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnLapTrinhWeb.Models
{
    public class LandlordApplication
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public AppUser? User { get; set; }

        public string? Phone { get; set; }
        public string? CitizenIdFrontImageUrl { get; set; }
        public string? CitizenIdBackImageUrl { get; set; }
        public string? SupportingDocumentUrls { get; set; }
        public string? Note { get; set; }

        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedByAdminId { get; set; }
        public AppUser? ReviewedByAdmin { get; set; }

        public string? RejectionReason { get; set; }

        [NotMapped]
        public string[] SupportingDocumentUrlList
        {
            get => string.IsNullOrWhiteSpace(SupportingDocumentUrls)
                ? Array.Empty<string>()
                : SupportingDocumentUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            set => SupportingDocumentUrls = value == null || value.Length == 0
                ? null
                : string.Join(';', value);
        }

        [NotMapped]
        public bool IsPending => Status == "Pending";

        [NotMapped]
        public bool IsApproved => Status == "Approved";

        [NotMapped]
        public bool IsRejected => Status == "Rejected";
    }
}
