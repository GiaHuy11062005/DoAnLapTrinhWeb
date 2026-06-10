using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DoAnLapTrinhWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace DoAnLapTrinhWeb.Data
{
    public static class SampleAccommodationSeeder
    {
        private sealed record SeedRoom(
            string Title,
            string Description,
            decimal Price,
            double Area,
            string Address,
            string CityName,
            string DistrictKeyword,
            bool AllowsPets,
            bool HasParking,
            string ContactPhone,
            int DaysAgo);

        public static async Task SeedAsync(AppDbContext context)
        {
            if (await context.Accommodations.AnyAsync(a => a.Title != null && a.Title.StartsWith("MẪU -")))
            {
                return;
            }

            var admin = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@tienichtro.com");
            if (admin == null)
            {
                return;
            }

            var seeds = new List<SeedRoom>
            {
                new("MẪU - Trọ gần HCMUT", "Phòng sạch sẽ, phù hợp sinh viên Bách Khoa, có gác lửng và wifi ổn định.", 3200000m, 22, "127 Lý Thường Kiệt, Quận 10", "Thành phố Hồ Chí Minh", "Quận 10", true, true, "0901 111 222", 8),
                new("MẪU - Trọ gần UEL", "Phòng mới sơn sửa, yên tĩnh, thuận tiện di chuyển sang khu Đại học Kinh tế - Luật.", 2800000m, 20, "25 Võ Văn Ngân, TP. Thủ Đức", "Thành phố Hồ Chí Minh", "Thủ Đức", false, true, "0902 222 333", 6),
                new("MẪU - Trọ gần Nông Lâm", "Phòng thoáng mát, phù hợp sinh viên Nông Lâm, có chỗ để xe riêng.", 2500000m, 18, "88 Quốc lộ 1K, TP. Thủ Đức", "Thành phố Hồ Chí Minh", "Thủ Đức", true, true, "0903 333 444", 7),
                new("MẪU - Trọ gần HUTECH", "Khu trọ an ninh, gần HUTECH, đi Bình Thạnh rất tiện.", 3400000m, 24, "14 Ung Văn Khiêm, Bình Thạnh", "Thành phố Hồ Chí Minh", "Bình Thạnh", false, true, "0904 444 555", 5),
                new("MẪU - Trọ gần FPT HCM", "Phòng full nội thất, phù hợp sinh viên FPT, gần trục đường lớn.", 2600000m, 19, "32 Lê Văn Chí, TP. Thủ Đức", "Thành phố Hồ Chí Minh", "Thủ Đức", true, false, "0905 555 666", 4),
                new("MẪU - Trọ trung tâm Đà Nẵng", "Phòng giá mềm, tiện đi lại khu trung tâm thành phố Đà Nẵng.", 2200000m, 17, "19 Nguyễn Văn Linh, Hải Châu", "Đà Nẵng", "Hải Châu", false, true, "0906 666 777", 3),
                new("MẪU - Trọ gần cầu Rồng", "Phòng cho người đi làm, gần cầu Rồng và khu ăn uống.", 2700000m, 21, "45 Trần Phú, Sơn Trà", "Đà Nẵng", "Sơn Trà", true, true, "0907 777 888", 2),
                new("MẪU - Trọ gần Cầu Giấy", "Phòng rộng, phù hợp sinh viên và người đi làm tại Hà Nội.", 2900000m, 23, "12 Xuân Thủy, Cầu Giấy", "Hà Nội", "Cầu Giấy", false, true, "0908 888 999", 1)
            };

            foreach (var seed in seeds)
            {
                var city = await context.Cities.FirstOrDefaultAsync(c => c.Name != null && c.Name.Contains(seed.CityName));
                if (city == null)
                {
                    continue;
                }

                var district = await context.Districts
                    .FirstOrDefaultAsync(d => d.CityId == city.Id && d.Name != null && d.Name.Contains(seed.DistrictKeyword));

                if (district == null)
                {
                    district = await context.Districts.FirstOrDefaultAsync(d => d.CityId == city.Id);
                }

                if (district == null)
                {
                    continue;
                }

                if (await context.Accommodations.AnyAsync(a => a.Title == seed.Title))
                {
                    continue;
                }

                context.Accommodations.Add(new Accommodation
                {
                    Title = seed.Title,
                    Description = seed.Description,
                    Price = seed.Price,
                    Area = seed.Area,
                    SpecificAddress = seed.Address,
                    CityId = city.Id,
                    DistrictId = district.Id,
                    AllowsPets = seed.AllowsPets,
                    HasParking = seed.HasParking,
                    ContactPhone = seed.ContactPhone,
                    IsApproved = true,
                    CreatedAt = DateTime.Now.AddDays(-seed.DaysAgo),
                    OwnerId = admin.Id,
                    Images = new List<AccommodationImage>()
                });
            }

            await context.SaveChangesAsync();
        }
    }
}
