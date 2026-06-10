using System.Collections.Generic;

namespace DoAnLapTrinhWeb.Models
{
    // Lớp đại diện cho một Tỉnh/Thành phố
    public class City
    {
        // Khóa chính
        public int Id { get; set; }
        
        // Tên Tỉnh/Thành phố (Ví dụ: "Hồ Chí Minh")
        public string? Name { get; set; } 
        
        // Mã code từ API lấy dữ liệu hành chính để đối chiếu
        public int ApiCode { get; set; } 
        
        // Link tìm kiếm mặc định trên Google Maps cho thành phố này
        public string? GoogleMapsUrl { get; set; } 

        // Quan hệ 1-N: Một Thành phố có nhiều Quận/Huyện
        public ICollection<District>? Districts { get; set; }
    }
}
