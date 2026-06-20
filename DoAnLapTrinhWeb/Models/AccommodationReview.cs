using System;
using System.ComponentModel.DataAnnotations;

namespace DoAnLapTrinhWeb.Models
{
    public class AccommodationReview
    {
        public int Id { get; set; }

        public int AccommodationId { get; set; }
        public int RentalRequestId { get; set; }
        public int ReviewerId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(600)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Accommodation? Accommodation { get; set; }
        public RentalRequest? RentalRequest { get; set; }
        public AppUser? Reviewer { get; set; }
    }
}
