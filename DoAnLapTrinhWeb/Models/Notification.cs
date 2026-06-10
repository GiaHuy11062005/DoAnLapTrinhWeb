using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnLapTrinhWeb.Models
{
    // Lớp đại diện cho một Chuông Thông Báo gửi đến người dùng
    public class Notification
    {
        // Khóa chính tự tăng
        [Key]
        public int Id { get; set; }

        // Khóa ngoại: ID của người sẽ nhận thông báo này
        public int UserId { get; set; }
        
        // Thuộc tính điều hướng tới bảng User
        [ForeignKey("UserId")]
        public virtual AppUser? User { get; set; }

        // Tiêu đề ngắn gọn của thông báo (Ví dụ: "Bạn có đơn đặt phòng mới")
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        // Nội dung chi tiết của thông báo
        [Required]
        public string Message { get; set; } = string.Empty;

        // Cờ đánh dấu người dùng đã nhấp vào đọc thông báo này chưa
        public bool IsRead { get; set; } = false;

        // Đường dẫn URL mà hệ thống sẽ chuyển hướng tới khi người dùng nhấp vào thông báo
        public string? ActionUrl { get; set; }

        // Thời gian tạo thông báo
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
