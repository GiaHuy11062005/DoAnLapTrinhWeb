using System.Collections.Generic;

namespace DoAnLapTrinhWeb.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public string? Role { get; set; }

        // --- CÁC TRƯỜNG MỚI THÊM ---
        public bool IsEmailConfirmed { get; set; } = false; // Xác thực email
        public string? EmailConfirmationToken { get; set; } // Mã token gửi qua mail

        // Địa chỉ để đề xuất trọ gần nhất
        public int? CityId { get; set; }
        public int? DistrictId { get; set; }
        public string? Phone { get; set; } // SĐT mặc định

        // Lưu định danh: "Student", "Worker", hoặc rỗng (chưa chọn)
        public string? UserIdentity { get; set; }

        // Nơi lưu địa chỉ cụ thể của trường học hoặc công ty
        public string? DestinationAddress { get; set; }
        public ICollection<Accommodation>? Accommodations { get; set; }
    }
}
