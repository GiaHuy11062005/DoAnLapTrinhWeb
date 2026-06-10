using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
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

        public AccommodationController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // =======================================================
        // --- HÀM QUẢN LÝ DANH SÁCH PHÒNG ĐÃ ĐĂNG CỦA CHỦ TRỌ ---
        // =======================================================
        [HttpGet]
        [Authorize(Roles = "Landlord")] // Bảo mật: Chỉ chủ trọ mới vào được trang quản lý của họ
        public async Task<IActionResult> Index()
        {
            // 1. Lấy ID của chủ trọ đang đăng nhập từ Cookie Claims
            var currentUserId = await ResolveCurrentUserIdAsync();
            if (!currentUserId.HasValue) return Unauthorized();

            // 2. Tải danh sách phòng của chủ trọ này kèm các bảng liên kết để hiện tên Tỉnh, Quận và Ảnh đại diện
            var myRooms = await _context.Accommodations
                .Include(a => a.City)
                .Include(a => a.District)
                .Include(a => a.Images)
                .Where(a => a.OwnerId == currentUserId.Value)
                .OrderByDescending(a => a.CreatedAt) // Phòng mới đăng hiển thị lên đầu danh sách
                .ToListAsync();

            // 3. Trả dữ liệu về giao diện View
            return View(myRooms);
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
        public async Task<IActionResult> Create(Accommodation model, List<IFormFile> uploadImages)
        {
            try
            {
                // 1. Lấy ID chủ trọ đang đăng nhập
                var currentUserId = await ResolveCurrentUserIdAsync();
                if (!currentUserId.HasValue) return Unauthorized();

                model.OwnerId = currentUserId.Value;
                model.CreatedAt = DateTime.Now;
                model.IsApproved = false; // Mặc định bài đăng cần Admin duyệt

                // Khởi tạo list ảnh
                model.Images = new List<AccommodationImage>();

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

                // 3. Lưu vào Database
                _context.Accommodations.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đăng bài thành công! Vui lòng chờ Admin duyệt.";
                return RedirectToAction("Index", "Home");
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
        public async Task<IActionResult> Search(int? cityId, int? districtId, int? priceRange, string? school, bool? parking, bool? pets)
        {
            // Lấy danh sách phòng ĐÃ DUYỆT
            var query = _context.Accommodations.Include(a => a.Images).Where(a => a.IsApproved);

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

            // Lọc theo trường đại học / khu vực gần trường
            if (!string.IsNullOrWhiteSpace(school))
            {
                query = ApplySchoolFilter(query, school);
            }

            // Lọc theo tiện ích thực sự có trong dữ liệu
            if (parking == true)
                query = query.Where(a => a.HasParking);

            if (pets == true)
                query = query.Where(a => a.AllowsPets);

            // Ghi Log Tìm Kiếm xuống Database
                if (cityId.HasValue || districtId.HasValue)
                {
                    int? currentUserId = null;
                    if (User.Identity != null && User.Identity.IsAuthenticated)
                    {
                        var resolvedUserId = await ResolveCurrentUserIdAsync();
                        currentUserId = resolvedUserId;
                    }

                    var log = new SearchLog
                    {
                    UserId = currentUserId,
                    CityId = cityId,
                    DistrictId = districtId,
                    CreatedAt = DateTime.Now
                };
                _context.SearchLogs.Add(log);
                await _context.SaveChangesAsync(); // Lưu log trong background
            }

            var results = await query.ToListAsync();
            return View(results);
        }

        private static IQueryable<Accommodation> ApplySchoolFilter(IQueryable<Accommodation> query, string school)
        {
            var normalized = school.Trim().ToUpperInvariant();

            return normalized switch
            {
                "HCMUT" => query.Where(a =>
                    (a.Title != null && (a.Title.Contains("HCMUT") || a.Title.Contains("Bách Khoa"))) ||
                    (a.Description != null && a.Description.Contains("Bách Khoa"))),
                "UEL" => query.Where(a =>
                    (a.Title != null && (a.Title.Contains("UEL") || a.Title.Contains("Kinh tế - Luật"))) ||
                    (a.Description != null && a.Description.Contains("Kinh tế - Luật"))),
                "NLU" => query.Where(a =>
                    (a.Title != null && (a.Title.Contains("Nông Lâm") || a.Title.Contains("NLU"))) ||
                    (a.Description != null && a.Description.Contains("Nông Lâm"))),
                "HUTECH" => query.Where(a =>
                    (a.Title != null && a.Title.Contains("HUTECH")) ||
                    (a.Description != null && a.Description.Contains("HUTECH"))),
                "FPT" => query.Where(a =>
                    (a.Title != null && (a.Title.Contains("FPT") || a.Title.Contains("FPT HCM"))) ||
                    (a.Description != null && a.Description.Contains("FPT"))),
                _ => query
            };
        }

        // --- HÀM XỬ LÝ TRANG CHI TIẾT (TASK 1) ---
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var room = await _context.Accommodations
                .Include(a => a.City)
                .Include(a => a.District)
                .Include(a => a.Images)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (room == null) return NotFound();

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

        private async Task<int?> ResolveCurrentUserIdAsync()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out var userId))
            {
                return userId;
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            return user?.Id;
        }
    }
}
