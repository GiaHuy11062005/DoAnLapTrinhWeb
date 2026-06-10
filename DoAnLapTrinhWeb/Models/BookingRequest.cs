using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnLapTrinhWeb.Models
{
    public class BookingRequest
    {
        [Key]
        public int Id { get; set; }

        public int AccommodationId { get; set; }
        [ForeignKey("AccommodationId")]
        public virtual Accommodation? Accommodation { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public virtual AppUser? Student { get; set; }

        public int LandlordId { get; set; }
        [ForeignKey("LandlordId")]
        public virtual AppUser? Landlord { get; set; }

        [StringLength(500)]
        public string? Message { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
