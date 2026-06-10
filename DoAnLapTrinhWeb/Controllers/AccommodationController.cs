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

        public AccommodationController(AppDbContext context, IWebHostEnvironment webHostEnvironment, IHubContext<AccommodationHub> hubContext)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _hubContext = hubContext;
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
                await _hubContext.Clients.Group("Admins").SendAsync("PendingAccommodationCreated", new
                {
                    id = model.Id
                });

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
        public async Task<IActionResult> Search(int? cityId, int? districtId, int? priceRange, string? keyword, bool recordHistory = false)
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

            var results = await query.ToListAsync();

            // Chỉ lưu khi người dùng chủ động tìm mới, đã đăng nhập và tìm thấy phòng.
            // Link xem lại từ lịch sử không gửi recordHistory nên sẽ không tạo log trùng.
            if (recordHistory &&
                results.Count > 0 &&
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
    }
}
