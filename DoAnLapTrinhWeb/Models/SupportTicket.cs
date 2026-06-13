using System;
using System.ComponentModel.DataAnnotations;

namespace DoAnLapTrinhWeb.Models
{
    public class SupportTicket
    {
        [Key]
        public int Id { get; set; }

        public int? UserId { get; set; }
        public AppUser? User { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email liên hệ")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập nội dung cần hỗ trợ")]
        public string Message { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending"; // Pending, InReview, Resolved, Rejected

        public string? AttachedImagePath { get; set; }

        public string? AdminResponse { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
