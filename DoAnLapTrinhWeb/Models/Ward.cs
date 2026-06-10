namespace DoAnLapTrinhWeb.Models
{
    // Lớp đại diện cho một Phường/Xã
    public class Ward
    {
        // Khóa chính tự tăng
        public int Id { get; set; }
        
        // Tên Phường/Xã (Ví dụ: "Phường 14")
        public string? Name { get; set; }
        
        // Mã code từ API hành chính (provinces.open-api.vn) để đồng bộ dữ liệu
        public int ApiCode { get; set; } 

        // Khóa ngoại liên kết tới Quận/Huyện chứa Phường này
        public int DistrictId { get; set; }
        
        // Thuộc tính điều hướng tới Quận/Huyện
        public District? District { get; set; }
    }
}
