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
            // Nếu đã có City rồi thì chỉ backfill link Google Maps cho bản ghi cũ,
            // và kiểm tra xem có cần seed Ward không.
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

                // Seed Ward nếu chưa có
                await SeedWardsAsync(context);
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

                // Parse JSON
                using var document = JsonDocument.Parse(jsonString);
                var root = document.RootElement;

                // Các mã code chuẩn từ API: Hà Nội (1), Đà Nẵng (48), TP.HCM (79)
                int[] targetCityCodes = { 1, 48, 79 };

                foreach (var cityElement in root.EnumerateArray())
                {
                    int cityCode = cityElement.GetProperty("code").GetInt32();

                    if (targetCityCodes.Contains(cityCode))
                    {
                        var city = new City
                        {
                            Name = cityElement.GetProperty("name").GetString() ?? "",
                            ApiCode = cityCode,
                            GoogleMapsUrl = BuildGoogleMapsUrl(cityElement.GetProperty("name").GetString() ?? ""),
                            Districts = new List<District>()
                        };

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

                        await context.Cities.AddAsync(city);
                    }
                }

                await context.SaveChangesAsync();
                Console.WriteLine("Đã đồng bộ thành công dữ liệu các quận/huyện thuộc Hà Nội, Đà Nẵng và TP.HCM từ API!");

                // Sau khi seed City+District xong thì seed Ward
                await SeedWardsAsync(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lấy dữ liệu API: {ex.Message}");
            }
        }

        /// <summary>
        /// Seed Phường/Xã từ API provinces.open-api.vn.
        /// Chỉ seed nếu bảng Wards chưa có dữ liệu.
        /// </summary>
        private static async Task SeedWardsAsync(AppDbContext context)
        {
            // Kiểm tra nếu Wards đã có dữ liệu thì bỏ qua
            try
            {
                if (await context.Wards.AnyAsync())
                {
                    return;
                }
            }
            catch
            {
                // Bảng chưa tồn tại (chưa migrate) → bỏ qua
                return;
            }

            var districts = await context.Districts.ToListAsync();
            if (!districts.Any()) return;

            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            int seededCount = 0;
            int batchSize = 0;

            foreach (var district in districts)
            {
                try
                {
                    // Gọi API lấy danh sách Phường/Xã theo mã quận/huyện
                    var response = await client.GetAsync($"https://provinces.open-api.vn/api/d/{district.ApiCode}?depth=2");
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("wards", out var wardsElement) || wardsElement.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var wardElement in wardsElement.EnumerateArray())
                    {
                        context.Wards.Add(new Ward
                        {
                            Name = wardElement.GetProperty("name").GetString() ?? "",
                            ApiCode = wardElement.GetProperty("code").GetInt32(),
                            DistrictId = district.Id
                        });
                        seededCount++;
                    }

                    batchSize++;

                    // Lưu mỗi 5 quận để tránh timeout
                    if (batchSize % 5 == 0)
                    {
                        await context.SaveChangesAsync();
                    }

                    // Delay nhỏ để không spam API
                    await Task.Delay(150);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi seed Ward cho quận {district.Name}: {ex.Message}");
                }
            }

            // Lưu phần còn lại
            if (seededCount > 0)
            {
                await context.SaveChangesAsync();
                Console.WriteLine($"Đã seed {seededCount} Phường/Xã thành công!");
            }
        }
    }
}
