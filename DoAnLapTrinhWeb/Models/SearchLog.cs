using System;

namespace DoAnLapTrinhWeb.Models
{
    // Lớp lưu trữ Lịch sử Tìm kiếm của người dùng (Để phục vụ cho trang Thống kê Admin)
    public class SearchLog
    {
        // Khóa chính
        public int Id { get; set; }
        
        // Khóa ngoại: Lưu lại ID người dùng đã tìm kiếm (Nếu có đăng nhập, null nếu là khách)
        public int? UserId { get; set; } 
        
        // Khóa ngoại: ID Tỉnh/Thành phố họ đã chọn trong bộ lọc
        public int? CityId { get; set; } 
        
        // Khóa ngoại: ID Quận/Huyện họ đã chọn trong bộ lọc
        public int? DistrictId { get; set; } 
        
        // Thời gian họ bấm nút tìm kiếm
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}