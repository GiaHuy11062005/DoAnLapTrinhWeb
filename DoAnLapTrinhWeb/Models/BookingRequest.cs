using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnLapTrinhWeb.Models
{
    // Lớp đại diện cho một Đơn yêu cầu đặt phòng
    public class BookingRequest
    {
        // Khóa chính tự tăng
        [Key]
        public int Id { get; set; }

        // Khóa ngoại liên kết tới Phòng trọ được đặt
        public int AccommodationId { get; set; }
        
        // Thuộc tính điều hướng để truy xuất thông tin phòng
        [ForeignKey("AccommodationId")]
        public virtual Accommodation? Accommodation { get; set; }

        // Khóa ngoại liên kết tới Sinh viên (Người đặt phòng)
        public int StudentId { get; set; }
        
        // Thuộc tính điều hướng truy xuất thông tin Sinh viên
        [ForeignKey("StudentId")]
        public virtual AppUser? Student { get; set; }

        // Khóa ngoại liên kết tới Chủ trọ (Người nhận đơn)
        public int LandlordId { get; set; }
        
        // Thuộc tính điều hướng truy xuất thông tin Chủ trọ
        [ForeignKey("LandlordId")]
        public virtual AppUser? Landlord { get; set; }

        // Lời nhắn gửi kèm của Sinh viên cho Chủ trọ (tối đa 500 ký tự)
        [StringLength(500)]
        public string? Message { get; set; }

        // Trạng thái của đơn đặt phòng (Mặc định là "Pending" - Chờ duyệt)
        // Các trạng thái khác: "Accepted" (Đồng ý), "Rejected" (Từ chối)
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; 

        // Thời gian tạo đơn đặt phòng
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
