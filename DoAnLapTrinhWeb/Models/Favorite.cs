using System.ComponentModel.DataAnnotations;

namespace DoAnLapTrinhWeb.Models
{
    public class Favorite
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public AppUser? User { get; set; }

        public int AccommodationId { get; set; }
        public Accommodation? Accommodation { get; set; }
    }
}