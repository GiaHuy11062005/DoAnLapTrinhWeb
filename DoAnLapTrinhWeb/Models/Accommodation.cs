using System;
using System.Collections.Generic;

namespace DoAnLapTrinhWeb.Models
{
    // Lớp đại diện cho một bài đăng phòng trọ trên hệ thống
    public class Accommodation
    {
        // Mã định danh duy nhất của phòng trọ
        public int Id { get; set; }

        // Tiêu đề bài đăng (Ví dụ: "Phòng trọ giá rẻ gần ĐH Bách Khoa")
        public string? Title { get; set; }

        // Mô tả chi tiết về phòng trọ (Nội thất, điện nước, giờ giấc...)
        public string? Description { get; set; }

        // Giá thuê mỗi tháng (VNĐ)
        public decimal Price { get; set; }

        // Diện tích phòng (m2)
        public double Area { get; set; }

        // --- CÁC TRƯỜNG ĐỊA CHỈ ---
        
        // Số nhà, tên đường cụ thể
        public string? SpecificAddress { get; set; }

        // Khóa ngoại liên kết tới bảng City (Thành phố)
        public int CityId { get; set; }
        
        // Khóa ngoại liên kết tới bảng District (Quận/Huyện)
        public int DistrictId { get; set; }

        // Thuộc tính điều hướng (Navigation Property) giúp Entity Framework tự động JOIN bảng City
        public virtual City? City { get; set; }

        // Thuộc tính điều hướng giúp Entity Framework tự động JOIN bảng District
        public virtual District? District { get; set; }

        // --- CÁC TRƯỜNG TIỆN ÍCH ---
        
        // Cờ cho biết chủ trọ có cho phép người thuê nuôi thú cưng không
        public bool AllowsPets { get; set; }
        
        // Cờ cho biết phòng trọ có bãi giữ xe không
        public bool HasParking { get; set; }
        
        // Số điện thoại liên hệ riêng cho bài đăng này (có thể khác sđt đăng ký của chủ)
        public string? ContactPhone { get; set; }

        // --- QUẢN LÝ TRẠNG THÁI ---
        
        // Trạng thái kiểm duyệt: Đã được Admin duyệt để hiển thị công khai hay chưa
        public bool IsApproved { get; set; }
        
        // Lý do bài đăng bị Admin khóa hoặc từ chối (chỉ hiện khi IsApproved = false)
        public string? RejectionReason { get; set; }
        
        // Thời điểm đăng bài
        public DateTime CreatedAt { get; set; }

        // --- THÔNG TIN CHỦ TRỌ ---
        
        // Khóa ngoại liên kết tới bảng User (Chỉ những User có Role = "Landlord" hoặc "Admin")
        public int OwnerId { get; set; }
        
        // Thuộc tính điều hướng giúp truy xuất thông tin người sở hữu bài đăng
        public virtual AppUser? Owner { get; set; }

        // --- HÌNH ẢNH ---
        
        // Danh sách các bức ảnh mô tả phòng trọ (1 Phòng trọ có Nhiều ảnh)
        // Dùng virtual để hỗ trợ Lazy Loading của Entity Framework
        public virtual ICollection<AccommodationImage>? Images { get; set; }
    }
}