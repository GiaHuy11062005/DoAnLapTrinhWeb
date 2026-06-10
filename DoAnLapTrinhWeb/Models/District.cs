namespace DoAnLapTrinhWeb.Models
{
    // Lớp đại diện cho một Quận/Huyện
    public class District
    {
        // Khóa chính
        public int Id { get; set; }
        
        // Tên Quận/Huyện (Ví dụ: "Quận 10")
        public string? Name { get; set; }
        
        // Mã code từ API hành chính
        public int ApiCode { get; set; }

        // Khóa ngoại liên kết tới Thành phố chứa Quận này
        public int CityId { get; set; }
        
        // Thuộc tính điều hướng tới Thành phố
        public City? City { get; set; }
    }
}