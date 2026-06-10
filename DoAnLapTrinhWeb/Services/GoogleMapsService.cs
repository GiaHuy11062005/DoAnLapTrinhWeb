using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DoAnLapTrinhWeb.Services
{
    public class DistanceResult
    {
        public bool Success { get; set; }
        public string DistanceText { get; set; } = "";   // Ví dụ: "5.2 km"
        public double DistanceKm { get; set; }           // Ví dụ: 5.2
        public string DurationText { get; set; } = "";  // Ví dụ: "18 phút"
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Dịch vụ tính khoảng cách MIỄN PHÍ sử dụng:
    /// 1. Nominatim (OpenStreetMap) để geocode địa chỉ → tọa độ
    /// 2. OSRM (Open Source Routing Machine) để tính khoảng cách đường thực tế
    /// Không cần API Key, phù hợp cho demo/học tập.
    /// </summary>
    public class GoogleMapsService
    {
        private readonly HttpClient _httpClient;

        public GoogleMapsService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Nominatim yêu cầu User-Agent để identify ứng dụng
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "StayEase-Demo/1.0 (university-project)");
            }
        }

        public async Task<DistanceResult> GetDistanceAsync(string origin, string destination)
        {
            try
            {
                // --- BƯỚC 1: Geocode địa chỉ xuất phát ---
                var originCoords = await GeocodeAddressAsync(origin);
                if (originCoords == null)
                {
                    return new DistanceResult
                    {
                        Success = false,
                        ErrorMessage = $"Không tìm được tọa độ cho địa chỉ: \"{origin}\". Thử thêm tên thành phố vào."
                    };
                }

                // --- BƯỚC 2: Geocode địa chỉ đích ---
                var destCoords = await GeocodeAddressAsync(destination);
                if (destCoords == null)
                {
                    return new DistanceResult
                    {
                        Success = false,
                        ErrorMessage = $"Không tìm được tọa độ cho địa chỉ: \"{destination}\". Thử thêm tên thành phố vào."
                    };
                }

                // --- BƯỚC 3: Gọi OSRM để tính khoảng cách đường thực tế ---
                var (originLon, originLat) = originCoords.Value;
                var (destLon, destLat) = destCoords.Value;

                // Format: lon,lat;lon,lat (OSRM dùng thứ tự lon trước lat)
                var osrmUrl = $"https://router.project-osrm.org/route/v1/driving/{originLon},{originLat};{destLon},{destLat}?overview=false";

                var osrmResponse = await _httpClient.GetAsync(osrmUrl);
                osrmResponse.EnsureSuccessStatusCode();

                var osrmJson = await osrmResponse.Content.ReadAsStringAsync();
                using var osrmDoc = JsonDocument.Parse(osrmJson);
                var osrmRoot = osrmDoc.RootElement;

                var code = osrmRoot.GetProperty("code").GetString();
                if (code != "Ok")
                {
                    // Fallback về khoảng cách đường chim bay nếu OSRM thất bại
                    return FallbackHaversine(originLat, originLon, destLat, destLon);
                }

                var routes = osrmRoot.GetProperty("routes");
                if (routes.GetArrayLength() == 0)
                {
                    return FallbackHaversine(originLat, originLon, destLat, destLon);
                }

                var route = routes[0];
                var distanceMeters = route.GetProperty("distance").GetDouble();
                var durationSeconds = route.GetProperty("duration").GetDouble();

                double distanceKm = Math.Round(distanceMeters / 1000.0, 1);
                int durationMinutes = (int)Math.Ceiling(durationSeconds / 60.0);

                string distanceText = distanceKm >= 1
                    ? $"{distanceKm:F1} km"
                    : $"{(int)distanceMeters} m";

                string durationText = durationMinutes >= 60
                    ? $"{durationMinutes / 60} giờ {durationMinutes % 60} phút"
                    : $"{durationMinutes} phút";

                return new DistanceResult
                {
                    Success = true,
                    DistanceText = distanceText,
                    DistanceKm = distanceKm,
                    DurationText = durationText
                };
            }
            catch (Exception ex)
            {
                return new DistanceResult
                {
                    Success = false,
                    ErrorMessage = $"Lỗi kết nối dịch vụ bản đồ: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Geocode địa chỉ tiếng Việt → (longitude, latitude) sử dụng Nominatim OpenStreetMap
        /// </summary>
        private async Task<(double Lon, double Lat)?> GeocodeAddressAsync(string address)
        {
            try
            {
                // Thêm ", Việt Nam" để tăng độ chính xác geocoding
                var fullAddress = address.Contains("Việt Nam") || address.Contains("Vietnam")
                    ? address
                    : address + ", Việt Nam";

                var encoded = Uri.EscapeDataString(fullAddress);
                var url = $"https://nominatim.openstreetmap.org/search?q={encoded}&format=json&limit=1&countrycodes=vn";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetArrayLength() == 0) return null;

                var first = root[0];
                double lat = double.Parse(first.GetProperty("lat").GetString() ?? "0",
                    System.Globalization.CultureInfo.InvariantCulture);
                double lon = double.Parse(first.GetProperty("lon").GetString() ?? "0",
                    System.Globalization.CultureInfo.InvariantCulture);

                return (lon, lat);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fallback: Tính khoảng cách đường chim bay bằng công thức Haversine
        /// (dùng khi OSRM không tìm được tuyến đường)
        /// </summary>
        private static DistanceResult FallbackHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Bán kính Trái Đất (km)
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distanceKm = Math.Round(R * c, 1);

            // Ước tính thời gian di chuyển trung bình 30 km/h trong thành phố
            int durationMinutes = (int)Math.Ceiling(distanceKm / 30.0 * 60);

            return new DistanceResult
            {
                Success = true,
                DistanceText = $"~{distanceKm:F1} km (đường chim bay)",
                DistanceKm = distanceKm,
                DurationText = $"~{durationMinutes} phút (ước tính)"
            };
        }

        private static double ToRad(double deg) => deg * Math.PI / 180;
    }
}
