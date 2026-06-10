using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Models;

namespace DoAnLapTrinhWeb.Data
{
    public class LocationSeeder
    {
        private static string BuildGoogleMapsUrl(string cityName)
        {
            return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(cityName)}";
        }

        public static async Task SeedLocationsAsync(AppDbContext context)
        {
            // Nếu đã có City rồi thì chỉ backfill link Google Maps cho bản ghi cũ.
            if (await context.Cities.AnyAsync())
            {
                var existingCities = await context.Cities.ToListAsync();
                var hasChanges = false;

                foreach (var city in existingCities)
                {
                    if (string.IsNullOrWhiteSpace(city.GoogleMapsUrl) && !string.IsNullOrWhiteSpace(city.Name))
                    {
                        city.GoogleMapsUrl = BuildGoogleMapsUrl(city.Name);
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    await context.SaveChangesAsync();
                }

                return;
            }

            using HttpClient client = new HttpClient();

            try
            {
                // GỌI API BẤT ĐỒNG BỘ: Lấy toàn bộ Tỉnh/Thành kèm theo Quận/Huyện (depth=2)
                var response = await client.GetAsync("https://provinces.open-api.vn/api/?depth=2");
                response.EnsureSuccessStatusCode();

                // Đọc nội dung JSON bất đồng bộ
                var jsonString = await response.Content.ReadAsStringAsync();

                // Parse JSON (Sử dụng using để giải phóng bộ nhớ sau khi parse xong)
                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;

                // Các mã code chuẩn từ API: Hà Nội (1), Đà Nẵng (48), TP.HCM (79)
                int[] targetCityCodes = { 1, 48, 79 };

                foreach (var cityElement in root.EnumerateArray())
                {
                    int cityCode = cityElement.GetProperty("code").GetInt32();

                    // LỌC THÔNG MINH: Chỉ lấy dữ liệu nếu thuộc 3 thành phố yêu cầu
                    if (targetCityCodes.Contains(cityCode))
                    {
                        var city = new City
                        {
                            Name = cityElement.GetProperty("name").GetString() ?? "",
                            ApiCode = cityCode,
                            GoogleMapsUrl = BuildGoogleMapsUrl(cityElement.GetProperty("name").GetString() ?? ""),
                            Districts = new List<District>()
                        };

                        // Duyệt mảng Quận/Huyện của riêng thành phố đó
                        if (cityElement.TryGetProperty("districts", out var districtsElement) && districtsElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var distElement in districtsElement.EnumerateArray())
                            {
                                city.Districts.Add(new District
                                {
                                    Name = distElement.GetProperty("name").GetString() ?? "",
                                    ApiCode = distElement.GetProperty("code").GetInt32()
                                });
                            }
                        }

                        // THÊM VÀO DATABASE BẤT ĐỒNG BỘ
                        await context.Cities.AddAsync(city);
                    }
                }

                // LƯU THAY ĐỔI XUỐNG SQL SERVER
                await context.SaveChangesAsync();
                Console.WriteLine("Đã đồng bộ thành công dữ liệu các quận/huyện thuộc Hà Nội, Đà Nẵng và TP.HCM từ API!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy dữ liệu API: {ex.Message}");
            }
        }
    }
}
