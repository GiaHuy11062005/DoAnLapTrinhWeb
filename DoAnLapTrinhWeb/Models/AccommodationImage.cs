namespace DoAnLapTrinhWeb.Models
{
    // Lớp đại diện cho một Bức ảnh mô tả Phòng trọ
    public class AccommodationImage
    {
        // Khóa chính
        public int Id { get; set; }
        
        // Đường dẫn tương đối lưu ảnh trong hệ thống (Ví dụ: /images/accommodations/abc.jpg)
        public string? ImageUrl { get; set; }
        
        // Khóa ngoại: Ảnh này thuộc về Phòng trọ nào?
        public int AccommodationId { get; set; }
        
        // Thuộc tính điều hướng truy xuất tới phòng trọ
        public Accommodation? Accommodation { get; set; }
    }
}