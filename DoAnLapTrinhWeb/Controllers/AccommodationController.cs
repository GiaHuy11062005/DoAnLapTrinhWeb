using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using DoAnLapTrinhWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace DoAnLapTrinhWeb.Controllers
{
    public class AccommodationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // Dùng để lấy đường dẫn lưu ảnh
        private readonly IHubContext<AccommodationHub> _hubContext;
        private readonly IHttpClientFactory _httpClientFactory;

        public AccommodationController(
            AppDbContext context,
            IWebHostEnvironment webHostEnvironment,
            IHubContext<AccommodationHub> hubContext,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _hubContext = hubContext;
            _httpClientFactory = httpClientFactory;
        }

        // =======================================================
        // --- HÀM QUẢN LÝ DANH SÁCH PHÒNG ĐÃ ĐĂNG CỦA CHỦ TRỌ ---
        // =======================================================
        [HttpGet]
        [Authorize(Roles = "Landlord")] // Bảo mật: Chỉ chủ trọ mới vào được trang quản lý của họ
        public async Task<IActionResult> Index()
        {
            // 1. Lấy ID của chủ trọ đang đăng nhập từ Cookie Claims
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int currentUserId = int.Parse(userIdString);

            // 2. Tải danh sách phòng của chủ trọ này kèm các bảng liên kết để hiện tên Tỉnh, Quận và Ảnh đại diện
            var myRooms = await _context.Accommodations
                .Include(a => a.City)
                .Include(a => a.District)
                .Include(a => a.Images)
                .Where(a => a.OwnerId == currentUserId)
                .OrderByDescending(a => a.CreatedAt) // Phòng mới đăng hiển thị lên đầu danh sách
                .ToListAsync();

            // 3. Trả dữ liệu về giao diện View
            return View(myRooms);
        }

        [HttpGet]
        [Authorize(Roles = "Landlord")]
        public async Task<IActionResult> Edit(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            var room = await _context.Accommodations
                .Include(a => a.Images)
                .FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == currentUserId);

            if (room == null) return NotFound();
            if (!room.IsApproved)
            {
                TempData["Error"] = "Chỉ có thể chỉnh sửa bài đăng sau khi Admin đã duyệt.";
                return RedirectToAction(nameof(Index));
            }

            await LoadEditLocations(room.CityId);
            return View(new EditAccommodationViewModel
            {
                Id = room.Id,
                Title = room.Title,
                Description = room.Description,
                SpecificAddress = room.SpecificAddress,
                GoogleMapsUrl = room.GoogleMapsUrl,
                CityId = room.CityId,
                DistrictId = room.DistrictId,
                MaxOccupants = room.MaxOccupants,
                AllowsPets = room.AllowsPets,
                HasParking = room.HasParking,
                HasHighSpeedWifi = room.HasHighSpeedWifi,
                HasInverterAirConditioner = room.HasInverterAirConditioner,
                HasWasherDryer = room.HasWasherDryer,
                Has24HourSecurity = room.Has24HourSecurity,
                HasBasementParking = room.HasBasementParking,
                HasElevator = room.HasElevator,
                HasRefrigerator = room.HasRefrigerator,
                HasBalcony = room.HasBalcony,
                ExistingImages = room.Images?.ToList() ?? new()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Landlord")]
        public async Task<IActionResult> Edit(EditAccommodationViewModel model)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            // Một số trình duyệt vẫn bind danh sách tệp nhưng chứa phần tử rỗng khi người dùng
            // không chọn ảnh mới. Chỉ coi các tệp thực sự có dữ liệu là yêu cầu thay ảnh.
            var replacementImages = model.UploadImages?
                .Where(file => file != null && file.Length > 0)
                .ToList() ?? new List<IFormFile>();

            var room = await _context.Accommodations
                .Include(a => a.Images)
                .FirstOrDefaultAsync(a => a.Id == model.Id && a.OwnerId == currentUserId);

            if (room == null) return NotFound();
            if (!room.IsApproved)
            {
                TempData["Error"] = "Chỉ có thể chỉnh sửa bài đăng sau khi Admin đã duyệt.";
                return RedirectToAction(nameof(Index));
            }

            var districtIsValid = await _context.Districts
                .AnyAsync(d => d.Id == model.DistrictId && d.CityId == model.CityId);
            if (!districtIsValid)
            {
                ModelState.AddModelError(nameof(model.DistrictId), "Quận/Huyện không thuộc Tỉnh/Thành phố đã chọn.");
            }

            if (!IsGoogleMapsUrl(model.GoogleMapsUrl))
            {
                ModelState.AddModelError(nameof(model.GoogleMapsUrl), "Vui lòng dán đúng liên kết vị trí được sao chép từ Google Maps.");
            }

            var allowedImageTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (replacementImages.Count > 5)
            {
                ModelState.AddModelError(nameof(model.UploadImages), "Chỉ được tải tối đa 5 hình ảnh.");
            }
            else if (replacementImages.Any(file =>
                file.Length > 10 * 1024 * 1024 ||
                !allowedImageTypes.Contains(file.ContentType)))
            {
                ModelState.AddModelError(nameof(model.UploadImages), "Ảnh chỉ nhận JPG, PNG hoặc WEBP và tối đa 10MB mỗi ảnh.");
            }

            if (!ModelState.IsValid)
            {
                model.ExistingImages = room.Images?.ToList() ?? new();
                await LoadEditLocations(model.CityId);
                return View(model);
            }

            room.Title = model.Title?.Trim();
            room.Description = model.Description?.Trim();
            room.SpecificAddress = model.SpecificAddress?.Trim();
            room.GoogleMapsUrl = await NormalizeGoogleMapsUrlAsync(model.GoogleMapsUrl);
            room.CityId = model.CityId;
            room.DistrictId = model.DistrictId;
            room.MaxOccupants = model.MaxOccupants;
            room.AllowsPets = model.AllowsPets;
            room.HasParking = model.HasParking;
            room.HasHighSpeedWifi = model.HasHighSpeedWifi;
            room.HasInverterAirConditioner = model.HasInverterAirConditioner;
            room.HasWasherDryer = model.HasWasherDryer;
            room.Has24HourSecurity = model.Has24HourSecurity;
            room.HasBasementParking = model.HasBasementParking;
            room.HasElevator = model.HasElevator;
            room.HasRefrigerator = model.HasRefrigerator;
            room.HasBalcony = model.HasBalcony;

            var oldImagePaths = new List<string>();
            if (replacementImages.Count > 0)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "accommodations");
                Directory.CreateDirectory(uploadsFolder);

                oldImagePaths = room.Images?
                    .Where(image => !string.IsNullOrWhiteSpace(image.ImageUrl))
                    .Select(image => Path.Combine(uploadsFolder, Path.GetFileName(image.ImageUrl)))
                    .ToList() ?? new();

                if (room.Images != null)
                {
                    _context.AccommodationImages.RemoveRange(room.Images);
                }

                room.Images = new List<AccommodationImage>();
                foreach (var file in replacementImages)
                {
                    var safeExtension = file.ContentType switch
                    {
                        "image/jpeg" => ".jpg",
                        "image/png" => ".png",
                        "image/webp" => ".webp",
                        _ => throw new InvalidOperationException("Loại ảnh không hợp lệ.")
                    };
                    var uniqueFileName = $"{Guid.NewGuid():N}{safeExtension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using var fileStream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(fileStream);
                    room.Images.Add(new AccommodationImage
                    {
                        ImageUrl = $"/images/accommodations/{uniqueFileName}"
                    });
                }
            }

            // Giấy tờ đất không được gán hoặc thay đổi trong chức năng chỉnh sửa này.
            await _context.SaveChangesAsync();

            foreach (var oldImagePath in oldImagePaths)
            {
                if (System.IO.File.Exists(oldImagePath)) System.IO.File.Delete(oldImagePath);
            }

            TempData["Success"] = "Đã cập nhật thông tin phòng trọ.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadEditLocations(int cityId)
        {
            ViewBag.Cities = await _context.Cities.OrderBy(city => city.Name).ToListAsync();
            ViewBag.Districts = await _context.Districts
                .Where(district => district.CityId == cityId)
                .OrderBy(district => district.Name)
                .ToListAsync();
        }

        private static bool IsGoogleMapsUrl(string? url)
        {
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            var host = uri.Host.ToLowerInvariant();
            return host == "maps.app.goo.gl" ||
                   host == "maps.google.com" ||
                   host == "goo.gl" ||
                   host == "google.com" ||
                   host.EndsWith(".google.com", StringComparison.Ordinal);
        }

        private async Task<string?> NormalizeGoogleMapsUrlAsync(string? url)
        {
            var normalizedUrl = url?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedUrl) ||
                !Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
            {
                return normalizedUrl;
            }

            if (uri.Host.Equals("maps.app.goo.gl", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("goo.gl", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(8);
                    using var response = await client.GetAsync(
                        uri,
                        HttpCompletionOption.ResponseHeadersRead);

                    var finalUrl = response.RequestMessage?.RequestUri?.ToString();
                    if (IsGoogleMapsUrl(finalUrl))
                    {
                        return finalUrl;
                    }
                }
                catch (HttpRequestException)
                {
                    // Google Maps không phản hồi thì vẫn giữ link người dùng đã nhập.
                }
                catch (TaskCanceledException)
                {
                    // Không để việc mở rộng link làm người dùng không thể lưu bài.
                }
            }

            return normalizedUrl;
        }

        // --- API LOAD DANH SÁCH THÀNH PHỐ CHO POPUP ---
        [HttpGet]
        public async Task<IActionResult> GetAllCities()
        {
            var cities = await _context.Cities.Select(c => new { id = c.Id, name = c.Name }).ToListAsync();
            return Json(cities);
        }

        // --- API LOAD QUẬN HUYỆN (KHÔNG LỌC PHÒNG TRỐNG NHƯ TRANG CHỦ, VÌ ĐÂY LÀ ĐĂNG BÀI MỚI) ---
        [HttpGet]
        public async Task<IActionResult> GetAllDistricts(int cityId)
        {
            var districts = await _context.Districts
                .Where(d => d.CityId == cityId)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();
            return Json(districts);
        }

        // --- HÀM XỬ LÝ ĐĂNG BÀI TỪ POPUP ---
        [HttpPost]
        [Authorize(Roles = "Landlord")] // Chỉ Chủ trọ mới được đăng
        public async Task<IActionResult> Create(Accommodation model, List<IFormFile> uploadImages, List<IFormFile> landDocuments)
        {
            try
            {
                if (uploadImages == null || uploadImages.Count == 0 || uploadImages.Count > 5)
                {
                    TempData["Error"] = "Vui lòng chọn từ 1 đến tối đa 5 hình ảnh phòng trọ.";
                    return RedirectToAction("Index", "Home");
                }

                if (landDocuments == null || landDocuments.Count == 0 || landDocuments.Count > 3)
                {
                    TempData["Error"] = "Vui lòng tải từ 1 đến tối đa 3 giấy tờ đất bản sao có đóng mộc.";
                    return RedirectToAction("Index", "Home");
                }

                var allowedDocumentTypes = new[] { "image/jpeg", "image/png", "application/pdf" };
                if (landDocuments.Any(file => file.Length <= 0 || file.Length > 10 * 1024 * 1024 || !allowedDocumentTypes.Contains(file.ContentType)))
                {
                    TempData["Error"] = "Giấy tờ chỉ nhận JPG, PNG hoặc PDF và tối đa 10MB mỗi tệp.";
                    return RedirectToAction("Index", "Home");
                }

                // 1. Lấy ID chủ trọ đang đăng nhập
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

                model.OwnerId = int.Parse(userIdString);
                model.CreatedAt = DateTime.Now;
                model.IsApproved = false; // Mặc định bài đăng cần Admin duyệt

                if (model.MaxOccupants < 1 || model.MaxOccupants > 20)
                {
                    TempData["Error"] = "Giới hạn số người ở phải từ 1 đến 20 người.";
                    return RedirectToAction("Index", "Home");
                }

                if (!IsGoogleMapsUrl(model.GoogleMapsUrl))
                {
                    TempData["Error"] = "Vui lòng dán đúng liên kết vị trí được sao chép từ Google Maps.";
                    return RedirectToAction("Index", "Home");
                }

                model.GoogleMapsUrl = await NormalizeGoogleMapsUrlAsync(model.GoogleMapsUrl);

                // Khởi tạo list ảnh
                model.Images = new List<AccommodationImage>();
                model.Documents = new List<AccommodationDocument>();

                // 2. Xử lý Upload hình ảnh bất đồng bộ
                if (uploadImages != null && uploadImages.Count > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "accommodations");

                    // Tạo thư mục nếu chưa tồn tại
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    foreach (var file in uploadImages)
                    {
                        if (file.Length > 0)
                        {
                            // Tạo tên file ngẫu nhiên để không bị trùng
                            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            // Lưu đường dẫn ảnh vào Model
                            model.Images.Add(new AccommodationImage { ImageUrl = "/images/accommodations/" + uniqueFileName });
                        }
                    }
                }

                // Giấy tờ được lưu ngoài wwwroot, chỉ Admin có quyền tải qua controller.
                string documentsFolder = Path.Combine(_webHostEnvironment.ContentRootPath, "App_Data", "land-documents");
                Directory.CreateDirectory(documentsFolder);
                foreach (var file in landDocuments)
                {
                    string extension = file.ContentType switch
                    {
                        "image/jpeg" => ".jpg",
                        "image/png" => ".png",
                        "application/pdf" => ".pdf",
                        _ => throw new InvalidOperationException("Loại giấy tờ không hợp lệ.")
                    };
                    string storedFileName = $"{Guid.NewGuid():N}{extension}";
                    string filePath = Path.Combine(documentsFolder, storedFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    model.Documents.Add(new AccommodationDocument
                    {
                        OriginalFileName = Path.GetFileName(file.FileName),
                        StoredFileName = storedFileName,
                        ContentType = file.ContentType,
                        FileSize = file.Length
                    });
                }

                // 3. Lưu vào Database
                _context.Accommodations.Add(model);
                await _context.SaveChangesAsync();

                var adminIds = await _context.Users
                    .Where(user => user.Role == "Admin")
                    .Select(user => user.Id)
                    .ToListAsync();
                var adminNotifications = adminIds.Select(adminId => new AppNotification
                {
                    UserId = adminId,
                    Type = "PendingApproval",
                    Title = "Có bài đăng mới chờ duyệt",
                    Message = model.Title ?? "Bài đăng phòng trọ mới",
                    Url = "/Admin"
                }).ToList();
                _context.Notifications.AddRange(adminNotifications);
                await _context.SaveChangesAsync();

                foreach (var notification in adminNotifications)
                {
                    await SendNotification(notification);
                }

                await _hubContext.Clients.Group("Admins").SendAsync("PendingAccommodationCreated", new
                {
                    id = model.Id
                });

                TempData["Success"] = "Đăng bài thành công! Vui lòng chờ Admin duyệt.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi đăng bài: " + ex.Message);
                TempData["Error"] = "Có lỗi xảy ra trong quá trình đăng bài.";
                return RedirectToAction("Index", "Home");
            }
        }

        // --- HÀM XỬ LÝ TRANG KẾT QUẢ TÌM KIẾM (TASK 2) VÀ GHI LOG (TASK 4) ---
        [HttpGet]
        [AllowAnonymous] // Ai cũng có thể tìm kiếm
        public async Task<IActionResult> Search(int? cityId, int? districtId, int? priceRange, string? keyword, bool recordHistory = false, int page = 1)
        {
            // Lấy danh sách phòng ĐÃ DUYỆT
            var query = _context.Accommodations.Include(a => a.Images).Where(a => a.IsApproved);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                query = query.Where(a =>
                    (a.Title != null && a.Title.Contains(normalizedKeyword)) ||
                    (a.Description != null && a.Description.Contains(normalizedKeyword)) ||
                    (a.SpecificAddress != null && a.SpecificAddress.Contains(normalizedKeyword)));
            }

            // Lọc theo Thành phố
            if (cityId.HasValue)
                query = query.Where(a => a.CityId == cityId);

            // Lọc theo Quận/Huyện
            if (districtId.HasValue)
                query = query.Where(a => a.DistrictId == districtId);

            // Lọc theo Giá
            if (priceRange.HasValue)
            {
                if (priceRange == 1) query = query.Where(a => a.Price < 2000000);
                else if (priceRange == 2) query = query.Where(a => a.Price >= 2000000 && a.Price <= 4000000);
                else if (priceRange == 3) query = query.Where(a => a.Price > 4000000);
            }

            // --- THÊM PHẦN TÍNH TOÁN PHÂN TRANG ---
            int pageSize = 6;
            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            // Lưu lại tham số tìm kiếm để gán vào các nút bấm chuyển trang
            ViewBag.SearchCityId = cityId;
            ViewBag.SearchDistrictId = districtId;
            ViewBag.SearchPriceRange = priceRange;
            ViewBag.SearchKeyword = keyword;

            var results = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Chỉ lưu khi người dùng chủ động tìm mới, đã đăng nhập và tìm thấy phòng.
            // Link xem lại từ lịch sử không gửi recordHistory nên sẽ không tạo log trùng.
            if (recordHistory &&
                totalCount > 0 &&
                User.Identity?.IsAuthenticated == true &&
                (cityId.HasValue || districtId.HasValue))
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdString, out int currentUserId))
                {
                    var log = new SearchLog
                    {
                        UserId = currentUserId,
                        CityId = cityId,
                        DistrictId = districtId,
                        CreatedAt = DateTime.Now
                    };
                    _context.SearchLogs.Add(log);
                    await _context.SaveChangesAsync();

                    // Làm sạch cờ ghi lịch sử khỏi URL để refresh trang không tạo log trùng.
                    return RedirectToAction(nameof(Search), new
                    {
                        cityId,
                        districtId,
                        priceRange,
                        keyword
                    });
                }
            }

            return View(results);
        }

        // --- HÀM XỬ LÝ TRANG CHI TIẾT (TASK 1) ---
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var roomQuery = _context.Accommodations
                .Include(a => a.City)
                .Include(a => a.District)
                .Include(a => a.Images)
                .Include(a => a.Owner)
                .AsQueryable();

            // Khách chỉ được xem phòng đã duyệt. Admin và chính chủ vẫn được xem trước bài chờ duyệt.
            if (!User.IsInRole("Admin"))
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdString, out int currentUserId))
                {
                    roomQuery = roomQuery.Where(a => a.IsApproved || a.OwnerId == currentUserId);
                }
                else
                {
                    roomQuery = roomQuery.Where(a => a.IsApproved);
                }
            }

            var room = await roomQuery.FirstOrDefaultAsync(m => m.Id == id);

            if (room == null) return NotFound();

            // Bài cũ có thể đang lưu link chia sẻ rút gọn. Mở rộng link trong bộ nhớ
            // để preview vẫn lấy được tọa độ ghim chính xác khi người dùng mở bản đồ.
            room.GoogleMapsUrl = await NormalizeGoogleMapsUrlAsync(room.GoogleMapsUrl);

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

                if (currentUser != null)
                {
                    ViewBag.UserDestination = currentUser.DestinationAddress;
                }
            }

            return View(room);
        }

        private async Task SendNotification(AppNotification notification)
        {
            await _hubContext.Clients.Group($"User_{notification.UserId}").SendAsync("NotificationReceived", new
            {
                notification.Id,
                notification.Title,
                notification.Message,
                notification.Url,
                notification.Type,
                notification.IsRead,
                createdAt = notification.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            });
        }
    }
}
