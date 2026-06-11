using System;

namespace DoAnLapTrinhWeb.Models
{
    public class SearchLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; } // Lưu ID người dùng (nếu đã đăng nhập)
        public int? CityId { get; set; } // Lưu Tỉnh/Thành họ đã tìm
        public int? DistrictId { get; set; } // Lưu Quận/Huyện họ đã tìm
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}