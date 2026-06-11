using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DoAnLapTrinhWeb.Models
{
    public class EditAccommodationViewModel
    {
        public int Id { get; set; }

        [Required]
        public string? Title { get; set; }

        [Required]
        public string? Description { get; set; }

        [Required]
        public string? SpecificAddress { get; set; }

        [Range(1, int.MaxValue)]
        public int CityId { get; set; }

        [Range(1, int.MaxValue)]
        public int DistrictId { get; set; }

        public bool AllowsPets { get; set; }
        public bool HasParking { get; set; }
        public bool HasHighSpeedWifi { get; set; }
        public bool HasInverterAirConditioner { get; set; }
        public bool HasWasherDryer { get; set; }
        public bool Has24HourSecurity { get; set; }
        public bool HasBasementParking { get; set; }
        public bool HasElevator { get; set; }
        public bool HasRefrigerator { get; set; }
        public bool HasBalcony { get; set; }

        public List<IFormFile> UploadImages { get; set; } = new();
        public List<AccommodationImage> ExistingImages { get; set; } = new();
    }
}
