using System.ComponentModel.DataAnnotations;

namespace DoAnLapTrinhWeb.Models
{
    // Bảng trung gian lưu danh sách các phòng trọ được người dùng Yêu thích (Thả tim)
    public class Favorite
    {
        // Khóa chính của bản ghi lưu
        [Key]
        public int Id { get; set; }

        // Khóa ngoại: Ai là người đã lưu bài này?
        public int UserId { get; set; }
        
        // Thuộc tính điều hướng truy xuất tới User
        public AppUser? User { get; set; }

        // Khóa ngoại: Bài đăng phòng trọ nào được lưu?
        public int AccommodationId { get; set; }
        
        // Thuộc tính điều hướng truy xuất tới bài đăng Phòng trọ
        public Accommodation? Accommodation { get; set; }
    }
}