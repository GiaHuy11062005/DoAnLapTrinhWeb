namespace DoAnLapTrinhWeb.Models
{
    public class Ward
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int ApiCode { get; set; } // Mã code từ API provinces.open-api.vn

        // Foreign Key
        public int DistrictId { get; set; }
        public District? District { get; set; }
    }
}
