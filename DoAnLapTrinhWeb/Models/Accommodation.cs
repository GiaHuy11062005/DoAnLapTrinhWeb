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

        // Đối tượng ưu tiên (VD: All, Student, Worker)
        public string? TargetAudience { get; set; }

        // Phân loại hình thức cho thuê
        // Các giá trị đề xuất: "PhongTro" (Phòng trọ), "NhaNguyenCan" (Nhà nguyên căn), "CanHo" (Căn hộ mini), "SleepBox" (Ký túc xá/Sleepbox)
        public string? AccommodationType { get; set; } = "PhongTro"; // Mặc định là Phòng trọ để không lỗi dữ liệu cũ

        // Khóa ngoại lưu mã ID
        public int CityId { get; set; }
        public int DistrictId { get; set; }

        public int AvailableRooms { get; set; } = 1; // Dành cho Phòng trọ/Ký túc xá (Mặc định là 1)

        public bool IsRented { get; set; } = false;  // Dành cho Nhà nguyên căn/Căn hộ (false = Đang cho thuê)

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
        public bool IsRejected { get; set; } = false;
        public string? RejectionReason { get; set; }
        public bool IsHidden { get; set; } = false; // Ẩn bài đăng trên trang chủ
        public DateTime CreatedAt { get; set; }

        // --- CHO THUÊ / TRẠNG THÁI ---
        public bool IsRentedOut { get; set; } = false; // true = Hết phòng / Đã có người thuê

        // --- THÔNG TIN CHỦ TRỌ ---
        public int OwnerId { get; set; }
        public virtual AppUser? Owner { get; set; }

        // --- HÌNH ẢNH ---
        // Đã thêm chữ virtual để đồng bộ với cơ chế của Entity Framework
        public virtual ICollection<AccommodationImage>? Images { get; set; }
        public virtual ICollection<AccommodationDocument>? Documents { get; set; }
        public virtual ICollection<AccommodationReview>? Reviews { get; set; }
    }
}
