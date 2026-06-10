using System.Collections.Generic;

namespace DoAnLapTrinhWeb.Models
{
    // Lớp đại diện cho một Tài khoản Người dùng trên hệ thống
    public class AppUser
    {
        // Mã định danh duy nhất của người dùng
        public int Id { get; set; }
        
        // Họ và tên đầy đủ
        public string? FullName { get; set; }
        
        // Địa chỉ email dùng để đăng nhập
        public string? Email { get; set; }
        
        // Mật khẩu đã được mã hóa (băm) để bảo mật
        public string? PasswordHash { get; set; }
        
        // Quyền hạn của người dùng (Ví dụ: "Student", "Landlord", "Admin")
        public string? Role { get; set; }

        // --- CÁC TRƯỜNG BẢO MẬT VÀ XÁC THỰC ---
        
        // Cờ kiểm tra xem email đã được xác minh chưa
        public bool IsEmailConfirmed { get; set; } = false;
        
        // Chuỗi token gửi qua email để người dùng bấm vào xác minh
        public string? EmailConfirmationToken { get; set; }

        // --- THÔNG TIN LIÊN HỆ & ĐỊA CHỈ ---
        
        // ID Thành phố hiện tại của người dùng
        public int? CityId { get; set; }
        
        // ID Quận/Huyện hiện tại của người dùng
        public int? DistrictId { get; set; }
        
        // Số điện thoại mặc định
        public string? Phone { get; set; }

        // --- ĐỊNH DANH CÁ NHÂN ---
        
        // Loại định danh: "Student" (Sinh viên), "Worker" (Người đi làm), hoặc rỗng
        public string? UserIdentity { get; set; }

        // Tên trường đại học hoặc công ty để gợi ý trọ gần đó
        public string? DestinationAddress { get; set; }
        
        // --- LIÊN KẾT DỮ LIỆU ---
        
        // Danh sách các phòng trọ mà người dùng này (nếu là Chủ trọ) đang sở hữu
        public ICollection<Accommodation>? Accommodations { get; set; }
    }
}