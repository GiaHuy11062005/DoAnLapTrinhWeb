using System.Collections.Generic;

namespace DoAnLapTrinhWeb.Models
{
    public class City
    {
        public int Id { get; set; }
        public string? Name { get; set; } // Tên thành phố
        public int ApiCode { get; set; } // Mã code từ API để đối chiếu

        // Relationship
        public ICollection<District>? Districts { get; set; }
    }
}