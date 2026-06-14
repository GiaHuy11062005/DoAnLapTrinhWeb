using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

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

        [Required]
        [Url]
        public string? GoogleMapsUrl { get; set; }

        public string? AccommodationType { get; set; }

        [Required]
        [Range(0, 1000000000, ErrorMessage = "Giá thuê không hợp lệ")]
        public decimal Price { get; set; }

        [Range(0, 1000)]
        public int AvailableRooms { get; set; } = 1;

        public bool IsRented { get; set; } = false;

        [Range(1, int.MaxValue)]
        public int CityId { get; set; }

        [Range(1, int.MaxValue)]
        public int DistrictId { get; set; }

        [Range(1, 20)]
        public int MaxOccupants { get; set; } = 1;

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

        public List<IFormFile>? UploadImages { get; set; }

        [ValidateNever]
        public List<AccommodationImage> ExistingImages { get; set; } = new();
    }
}
