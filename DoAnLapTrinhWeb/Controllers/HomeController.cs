using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Bắt buộc phải có để dùng Include, ToListAsync
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Security.Claims; // Để lấy thông tin đăng nhập

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;

namespace DoAnLapTrinhWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public HomeController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // Đổi thành async Task<IActionResult>
        public async Task<IActionResult> Index()
        {
            // 1. Load danh sách thành phố cho thanh tìm kiếm
            ViewBag.Cities = await _context.Cities.ToListAsync();
            ViewBag.HomeBanner = await LoadHomeBannerSettingsAsync();

            // 2. LOGIC ĐỀ XUẤT TRỌ GẦN NHẤT
            List<Accommodation> suggestedRooms = new List<Accommodation>();

            // Kiểm tra xem trình duyệt đã có session/cookie đăng nhập chưa
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                // Lấy ID của User đang đăng nhập (Từ Claims)
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    // Lấy thông tin địa chỉ của User này
                    var currentUser = await _context.Users.FindAsync(userId);

                    if (currentUser != null && currentUser.CityId.HasValue)
                    {
                        // Truy vấn phòng trọ: Cùng thành phố, đã duyệt
                        suggestedRooms = await _context.Accommodations
                            .Include(a => a.Images) // Lấy kèm list hình ảnh để hiển thị
                            .Include(a => a.City)
                            .Include(a => a.District)
                            .Include(a => a.Reviews)
                            .Where(a => a.IsApproved == true && a.CityId == currentUser.CityId)
                            .ToListAsync();
                        suggestedRooms = suggestedRooms
                            .OrderByDescending(a => a.DistrictId == currentUser.DistrictId)
                            .ThenByDescending(GetPositiveReviewScore)
                            .ThenByDescending(a => a.Reviews?.Count ?? 0)
                            .ThenByDescending(a => a.CreatedAt)
                            .Take(12)
                            .ToList();
                    }
                }
            }

            // Khách chưa đăng nhập hoặc chưa có địa chỉ vẫn thấy phòng nổi bật mới nhất.
            if (suggestedRooms.Count == 0)
            {
                suggestedRooms = await BuildFeaturedRoomsAsync(null, null, null, null, null, null, 12);
            }
            else
            {
                var suggestedIds = suggestedRooms.Select(room => room.Id).ToList();
                if (suggestedIds.Count < 12)
                {
                    var additionalRooms = await BuildFeaturedRoomsAsync(null, null, null, null, null, null, 12);
                    suggestedIds.AddRange(additionalRooms
                        .Where(room => !suggestedIds.Contains(room.Id))
                        .Select(room => room.Id)
                        .Take(12 - suggestedIds.Count));
                }

                suggestedRooms = await _context.Accommodations
                    .Include(a => a.Images)
                    .Include(a => a.City)
                    .Include(a => a.District)
                    .Include(a => a.Reviews)
                    .Where(a => suggestedIds.Contains(a.Id))
                    .ToListAsync();
                suggestedRooms = suggestedRooms
                    .OrderBy(room => suggestedIds.IndexOf(room.Id))
                    .ToList();
            }

            // Truyền danh sách đề xuất ra View
            ViewBag.SuggestedRooms = suggestedRooms;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> FeaturedRooms(int? cityId, int? districtId, double? radiusKm, string? keyword, int? priceRange, string? accommodationType)
        {
            var rooms = await BuildFeaturedRoomsAsync(cityId, districtId, radiusKm, keyword, priceRange, accommodationType, 12);
            return Json(new
            {
                success = true,
                count = rooms.Count,
                results = rooms.Select(room => new
                {
                    room.Id,
                    room.Title,
                    price = room.Price.ToString("N0"),
                    location = $"{room.District?.Name}, {room.City?.Name}",
                    area = room.Area,
                    imageUrl = room.Images?.FirstOrDefault()?.ImageUrl,
                    averageRating = GetAverageRating(room),
                    reviewCount = room.Reviews?.Count ?? 0,
                    room.HasParking,
                    room.AllowsPets
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetDistrictsWithRooms(int cityId)
        {
            // Luôn trả toàn bộ khu vực thuộc thành phố đã chọn.
            // Việc lọc phòng được thực hiện sau khi người dùng gửi form tìm kiếm.
            var districts = await _context.Districts
                .Where(d => d.CityId == cityId)
                .OrderBy(d => d.Name)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();

            return Json(districts);
        }

        [Authorize]
        public async Task<IActionResult> SearchHistory()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login", "Auth");
            }

            var logs = await _context.SearchLogs
                .Where(log => log.UserId == userId)
                .OrderByDescending(log => log.CreatedAt)
                .Take(50)
                .ToListAsync();

            ViewBag.Cities = await _context.Cities.ToDictionaryAsync(city => city.Id, city => city.Name ?? "");
            ViewBag.Districts = await _context.Districts.ToDictionaryAsync(district => district.Id, district => district.Name ?? "");
            return View(logs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteSearchHistory(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var log = await _context.SearchLogs.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (log != null)
            {
                _context.SearchLogs.Remove(log);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(SearchHistory));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ClearSearchHistory()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            var logs = await _context.SearchLogs.Where(item => item.UserId == userId).ToListAsync();
            if (logs.Count > 0)
            {
                _context.SearchLogs.RemoveRange(logs);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(SearchHistory));
        }

        private async Task<List<Accommodation>> BuildFeaturedRoomsAsync(int? cityId, int? districtId, double? radiusKm, string? keyword, int? priceRange, string? accommodationType, int take)
        {
            var query = _context.Accommodations
                .Include(room => room.Images)
                .Include(room => room.City)
                .Include(room => room.District)
                .Include(room => room.Reviews)
                .Where(room => room.IsApproved && !room.IsHidden);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                query = query.Where(room =>
                    (room.Title != null && room.Title.Contains(normalizedKeyword)) ||
                    (room.Description != null && room.Description.Contains(normalizedKeyword)) ||
                    (room.SpecificAddress != null && room.SpecificAddress.Contains(normalizedKeyword)));
            }

            if (!string.IsNullOrWhiteSpace(accommodationType))
            {
                query = accommodationType == "whole-house"
                    ? query.Where(room => room.AccommodationType == "NhaNguyenCan" || room.AccommodationType == "CanHo")
                    : query.Where(room => room.AccommodationType == "PhongTro" || room.AccommodationType == "SleepBox");
            }

            if (priceRange.HasValue)
            {
                query = priceRange.Value switch
                {
                    1 => query.Where(room => room.Price < 2000000),
                    2 => query.Where(room => room.Price >= 2000000 && room.Price <= 4000000),
                    3 => query.Where(room => room.Price > 4000000),
                    _ => query
                };
            }

            if (cityId.HasValue)
                query = query.Where(room => room.CityId == cityId.Value);

            if (districtId.HasValue)
                query = query.Where(room => room.DistrictId == districtId.Value);

            var rooms = await query
                .OrderByDescending(room => room.CreatedAt)
                .Take(60)
                .ToListAsync();

            if ((cityId.HasValue || districtId.HasValue) && radiusKm.HasValue && radiusKm.Value > 0)
            {
                var searchCenter = await ResolveSearchCenterAsync(cityId, districtId, rooms);
                if (!searchCenter.HasValue)
                    return new List<Accommodation>();

                rooms = rooms.Where(room =>
                {
                    if (!GoogleMapsUrlHelper.TryGetCoordinates(room.GoogleMapsUrl, out var latitude, out var longitude))
                        return false;
                    return MapDistanceHelper.CalculateKm(searchCenter.Value.Latitude, searchCenter.Value.Longitude, latitude, longitude) <= radiusKm.Value;
                }).ToList();
            }

            return rooms
                .OrderByDescending(GetPositiveReviewScore)
                .ThenByDescending(room => room.Reviews?.Count ?? 0)
                .ThenByDescending(room => room.CreatedAt)
                .Take(take)
                .ToList();
        }

        private async Task<(double Latitude, double Longitude)?> ResolveSearchCenterAsync(
            int? cityId,
            int? districtId,
            IEnumerable<Accommodation> rooms)
        {
            if (districtId.HasValue)
            {
                var district = await _context.Districts
                    .Include(item => item.City)
                    .FirstOrDefaultAsync(item => item.Id == districtId.Value);
                if (district != null &&
                    MapDistanceHelper.TryGetDistrictCenter(district.City?.Name, district.Name, out var districtLatitude, out var districtLongitude))
                    return (districtLatitude, districtLongitude);

                if (TryGetAverageRoomCenter(rooms, out var averageLatitude, out var averageLongitude))
                    return (averageLatitude, averageLongitude);
            }

            if (!cityId.HasValue) return null;
            var cityName = await _context.Cities
                .Where(city => city.Id == cityId.Value)
                .Select(city => city.Name)
                .FirstOrDefaultAsync();
            return MapDistanceHelper.TryGetCityCenter(cityName, out var cityLatitude, out var cityLongitude)
                ? (cityLatitude, cityLongitude)
                : null;
        }

        private static bool TryGetAverageRoomCenter(IEnumerable<Accommodation> rooms, out double latitude, out double longitude)
        {
            var points = rooms
                .Select(room => GoogleMapsUrlHelper.TryGetCoordinates(room.GoogleMapsUrl, out var roomLatitude, out var roomLongitude)
                    ? (Latitude: roomLatitude, Longitude: roomLongitude)
                    : ((double Latitude, double Longitude)?)null)
                .Where(point => point.HasValue)
                .Select(point => point!.Value)
                .ToList();

            latitude = 0;
            longitude = 0;
            if (points.Count == 0) return false;

            latitude = points.Average(point => point.Latitude);
            longitude = points.Average(point => point.Longitude);
            return true;
        }

        private static double GetPositiveReviewScore(Accommodation room)
        {
            var reviews = room.Reviews?.ToList() ?? new List<AccommodationReview>();
            if (reviews.Count == 0) return 0;

            var average = reviews.Average(review => review.Rating);
            var positiveRate = reviews.Count(review => review.Rating >= 4) / (double)reviews.Count;
            return average + Math.Min(reviews.Count, 10) * 0.08 + positiveRate * 0.4;
        }

        private static double GetAverageRating(Accommodation room)
        {
            var reviews = room.Reviews?.ToList() ?? new List<AccommodationReview>();
            return reviews.Count == 0 ? 0 : Math.Round(reviews.Average(review => review.Rating), 1);
        }

        private async Task<HomeBannerSettings> LoadHomeBannerSettingsAsync()
        {
            var filePath = GetHomeBannerSettingsPath();
            if (!System.IO.File.Exists(filePath)) return new HomeBannerSettings();

            try
            {
                var json = await System.IO.File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<HomeBannerSettings>(json) ?? new HomeBannerSettings();
            }
            catch
            {
                return new HomeBannerSettings();
            }
        }

        private string GetHomeBannerSettingsPath()
        {
            return Path.Combine(_environment.ContentRootPath, "App_Data", "home-banner.json");
        }
    }
}
