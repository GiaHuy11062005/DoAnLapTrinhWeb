using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnLapTrinhWeb.Models
{
    // Lớp đại diện cho đơn đăng ký trở thành chủ trọ
    public class LandlordApplication
    {
        // Mã đơn đăng ký
        public int Id { get; set; }

        // ID của người dùng gửi đơn
        public int UserId { get; set; }

        // Thông tin người dùng gửi đơn
        public AppUser? User { get; set; }

        // Số điện thoại liên hệ
        public string? Phone { get; set; }

        // Đường dẫn ảnh mặt trước CCCD/CMND
        public string? CitizenIdFrontImageUrl { get; set; }

        // Đường dẫn ảnh mặt sau CCCD/CMND
        public string? CitizenIdBackImageUrl { get; set; }

        // Danh sách URL giấy tờ bổ sung, lưu dưới dạng chuỗi ngăn cách bởi dấu ';'
        public string? SupportingDocumentUrls { get; set; }

        // Ghi chú của người đăng ký
        public string? Note { get; set; }

        // Trạng thái đơn đăng ký (Pending, Approved, Rejected)
        public string Status { get; set; } = "Pending";

        // Thời gian tạo đơn
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Thời gian admin xử lý đơn
        public DateTime? ReviewedAt { get; set; }

        // ID của admin đã duyệt hoặc từ chối đơn
        public int? ReviewedByAdminId { get; set; }

        // Thông tin admin xử lý đơn
        public AppUser? ReviewedByAdmin { get; set; }

        // Lý do từ chối đơn (nếu có)
        public string? RejectionReason { get; set; }

        // Thuộc tính không ánh xạ vào cơ sở dữ liệu
        [NotMapped]
        public string[] SupportingDocumentUrlList
        {
            // Chuyển chuỗi URL thành mảng URL
            get => string.IsNullOrWhiteSpace(SupportingDocumentUrls)
                ? Array.Empty<string>()
                : SupportingDocumentUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Chuyển mảng URL thành chuỗi để lưu vào database
            set => SupportingDocumentUrls = value == null || value.Length == 0
                ? null
                : string.Join(';', value);
        }

        // Kiểm tra đơn đang ở trạng thái chờ duyệt
        [NotMapped]
        public bool IsPending => Status == "Pending";

        // Kiểm tra đơn đã được duyệt
        [NotMapped]
        public bool IsApproved => Status == "Approved";

        // Kiểm tra đơn đã bị từ chối
        [NotMapped]
        public bool IsRejected => Status == "Rejected";
    }
}