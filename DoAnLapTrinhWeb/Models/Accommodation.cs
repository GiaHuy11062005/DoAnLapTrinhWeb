using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DoAnLapTrinhWeb.Models
{
    public class Accommodation
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public double Area { get; set; }

        [Range(1, 20)]
        public int MaxOccupants { get; set; } = 1;

        // --- CÁC TRƯỜNG ĐỊA CHỈ ---
        public string? SpecificAddress { get; set; } // Số nhà, tên đường chi tiết
        public string? GoogleMapsUrl { get; set; } // Link vị trí chính xác được sao chép từ Google Maps

        // Khóa ngoại lưu mã ID
        public int CityId { get; set; }
        public int DistrictId { get; set; }

        // Navigation Properties giúp EF Core JOIN bảng
        public virtual City? City { get; set; }
        public virtual District? District { get; set; }

        // --- CÁC TRƯỜNG TIỆN ÍCH KHÁC ---
        public bool AllowsPets { get; set; } // Cho phép nuôi thú cưng
        public bool HasParking { get; set; } // Có bãi giữ xe
        public bool HasHighSpeedWifi { get; set; }
        public bool HasInverterAirConditioner { get; set; }
        public bool HasWasherDryer { get; set; }
        public bool Has24HourSecurity { get; set; }
        public bool HasBasementParking { get; set; }
        public bool HasElevator { get; set; }
        public bool HasRefrigerator { get; set; }
        public bool HasBalcony { get; set; }
        public string? ContactPhone { get; set; } // SĐT liên hệ riêng cho bài này

        public bool IsApproved { get; set; }
        public DateTime CreatedAt { get; set; }

        // --- THÔNG TIN CHỦ TRỌ ---
        public int OwnerId { get; set; }
        public virtual AppUser? Owner { get; set; }

        // --- HÌNH ẢNH ---
        // Đã thêm chữ virtual để đồng bộ với cơ chế của Entity Framework
        public virtual ICollection<AccommodationImage>? Images { get; set; }
        public virtual ICollection<AccommodationDocument>? Documents { get; set; }
    }
}
