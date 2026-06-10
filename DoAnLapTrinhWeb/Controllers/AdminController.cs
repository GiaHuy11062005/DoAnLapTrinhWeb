using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using DoAnLapTrinhWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var pendingRooms = await _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.Images)
                .Where(a => a.IsApproved == false && string.IsNullOrEmpty(a.RejectionReason)) // Đang chờ duyệt thực sự
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var pendingApplications = await GetPendingApplicationsSafeAsync();

            var totalUsers = await _context.Users.CountAsync(u => u.Role != "Admin");
            var totalAccommodations = await _context.Accommodations.CountAsync();
            var pendingAccommodationsCount = await _context.Accommodations.CountAsync(a => a.IsApproved == false && string.IsNullOrEmpty(a.RejectionReason));
            
            // Lấy Top 5 thành phố được tìm kiếm nhiều nhất
            var topSearchedCities = await _context.SearchLogs
                .Where(s => s.CityId.HasValue)
                .GroupBy(s => s.CityId)
                .Select(g => new { CityId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            var topCityIds = topSearchedCities.Select(c => c.CityId).ToList();
            var cityNames = await _context.Cities.Where(c => topCityIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);

            var topSearchedCitiesList = topSearchedCities.Select(c => new SearchStat
            {
                Name = c.CityId.HasValue && cityNames.ContainsKey(c.CityId.Value) ? cityNames[c.CityId.Value] : "Không xác định",
                Count = c.Count
            }).ToList();

            // Số lượng phòng theo thành phố (Top 5)
            var accByCity = await _context.Accommodations
                .GroupBy(a => a.CityId)
                .Select(g => new { CityId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            var accCityIds = accByCity.Select(c => c.CityId).ToList();
            var accCityNames = await _context.Cities.Where(c => accCityIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);

            var accommodationsByCityList = accByCity.Select(c => new SearchStat
            {
                Name = accCityNames.ContainsKey(c.CityId) ? accCityNames[c.CityId] : "Không xác định",
                Count = c.Count
            }).ToList();

            return View(new AdminDashboardViewModel
            {
                PendingRooms = pendingRooms,
                PendingLandlordApplications = pendingApplications,
                TotalUsers = totalUsers,
                TotalAccommodations = totalAccommodations,
                PendingAccommodationsCount = pendingAccommodationsCount,
                PendingLandlordsCount = pendingApplications.Count,
                TopSearchedCities = topSearchedCitiesList,
                AccommodationsByCity = accommodationsByCityList
            });
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var room = await _context.Accommodations.FindAsync(id);
            if (room != null)
            {
                room.IsApproved = true;
                room.RejectionReason = null;
                
                // Tự động tạo và lưu một Thông báo (Notification) gửi đến Chủ trọ khi bài đăng được Admin duyệt
                var notification = new Notification
                {
                    UserId = room.OwnerId,
                    Title = "Phòng trọ đã được DUYỆT",
                    Message = $"Bài đăng phòng trọ '{room.Title}' của bạn đã được Admin kiểm duyệt và hiện đang hiển thị trên hệ thống.",
                    ActionUrl = $"/Accommodation/Details/{room.Id}",
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã duyệt bài đăng thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var room = await _context.Accommodations.FindAsync(id);
            if (room != null)
            {
                // Xóa danh sách yêu thích trước
                var favorites = _context.Favorites.Where(f => f.AccommodationId == id);
                _context.Favorites.RemoveRange(favorites);

                _context.Accommodations.Remove(room);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa bài đăng!";
            }

            // Trở về trang trước đó nếu có (có thể là Index hoặc Accommodations)
            var returnUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RejectAccommodation(int id, string reason)
        {
            var accommodation = await _context.Accommodations.FindAsync(id);
            if (accommodation != null)
            {
                accommodation.IsApproved = false;
                accommodation.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Không đạt yêu cầu" : reason;
                
                // Gửi thông báo từ chối bài đăng kèm lý do cụ thể để Chủ trọ biết
                var notification = new Notification
                {
                    UserId = accommodation.OwnerId,
                    Title = "Bài đăng phòng trọ bị TỪ CHỐI",
                    Message = $"Bài đăng '{accommodation.Title}' của bạn không được duyệt. Lý do: {accommodation.RejectionReason}",
                    ActionUrl = "/Accommodation/Index",
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã khóa/từ chối bài đăng!";
            }

            var returnUrl = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> PendingAccommodations()
        {
            var accommodations = await _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.City)
                .Include(a => a.District)
                .Where(a => !a.IsApproved && string.IsNullOrEmpty(a.RejectionReason))
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(accommodations);
        }

        [HttpGet]
        public async Task<IActionResult> PendingLandlords()
        {
            try
            {
                var applications = await _context.LandlordApplications
                    .Include(a => a.User)
                    .Where(a => a.Status == "Pending")
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

                return View(applications);
            }
            catch (Exception ex) when (ex.Message.Contains("Invalid object name 'LandlordApplications'"))
            {
                // Trả về danh sách rỗng nếu bảng chưa được tạo trong CSDL
                return View(new List<DoAnLapTrinhWeb.Models.LandlordApplication>());
            }
        }



        [HttpGet]
        public async Task<IActionResult> Accommodations()
        {
            var accommodations = await _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.City)
                .Include(a => a.District)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(accommodations);
        }

        [HttpGet]
        public async Task<IActionResult> LandlordApplicationDetails(int id)
        {
            var application = await GetLandlordApplicationSafeAsync(id);

            if (application == null)
            {
                return NotFound();
            }

            return View(application);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveLandlordApplication(int id)
        {
            var application = await GetLandlordApplicationSafeAsync(id);

            if (application == null)
            {
                return NotFound();
            }

            application.Status = "Approved";
            application.ReviewedAt = DateTime.Now;
            application.ReviewedByAdminId = GetCurrentAdminId();
            application.RejectionReason = null;

            if (application.User != null)
            {
                application.User.Role = "Landlord";
                
                // Gửi thông báo chúc mừng người dùng khi hồ sơ Chủ trọ của họ được xét duyệt thành công
                var notification = new Notification
                {
                    UserId = application.User.Id,
                    Title = "Hồ sơ đăng ký Chủ trọ ĐƯỢC DUYỆT",
                    Message = "Chúc mừng! Bạn đã trở thành Chủ trọ. Giờ đây bạn có thể đăng tin phòng trọ của mình lên hệ thống.",
                    ActionUrl = "/Accommodation/Create",
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã duyệt yêu cầu đăng ký làm chủ trọ.";
            return RedirectToAction(nameof(LandlordApplicationDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RejectLandlordApplication(int id, string? rejectionReason)
        {
            var application = await GetLandlordApplicationSafeAsync(id);

            if (application == null)
            {
                return NotFound();
            }

            application.Status = "Rejected";
            application.ReviewedAt = DateTime.Now;
            application.ReviewedByAdminId = GetCurrentAdminId();
            application.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason)
                ? "Không đạt yêu cầu."
                : rejectionReason.Trim();

            // Gửi thông báo báo buồn cho người dùng khi hồ sơ Chủ trọ bị từ chối
            var notification = new Notification
            {
                UserId = application.UserId,
                Title = "Hồ sơ Chủ trọ BỊ TỪ CHỐI",
                Message = $"Yêu cầu đăng ký làm chủ trọ của bạn đã bị từ chối. Lý do: {application.RejectionReason}",
                ActionUrl = "/Profile/Update",
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã từ chối yêu cầu đăng ký làm chủ trọ.";
            return RedirectToAction(nameof(LandlordApplicationDetails), new { id });
        }

        private int? GetCurrentAdminId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .Where(u => u.Role != "Admin")
                .OrderByDescending(u => u.Id)
                .ToListAsync();

            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Users));
            }

            if (user.Role == "Admin")
            {
                TempData["Error"] = "Không thể xóa tài khoản Admin.";
                return RedirectToAction(nameof(Users));
            }

            // Xóa các dữ liệu liên quan trước
            var favorites = _context.Favorites.Where(f => f.UserId == id);
            _context.Favorites.RemoveRange(favorites);

            var accommodations = _context.Accommodations.Where(a => a.OwnerId == id);
            _context.Accommodations.RemoveRange(accommodations);

            try
            {
                var applications = _context.LandlordApplications.Where(a => a.UserId == id);
                _context.LandlordApplications.RemoveRange(applications);
            }
            catch { }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa tài khoản {user.FullName} ({user.Email}).";
            return RedirectToAction(nameof(Users));
        }

        private async Task<List<LandlordApplication>> GetPendingApplicationsSafeAsync()
        {
            try
            {
                return await _context.LandlordApplications
                    .Include(a => a.User)
                    .Where(a => a.Status == "Pending")
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex) when (ex.Message.Contains("Invalid object name 'LandlordApplications'"))
            {
                return new List<LandlordApplication>();
            }
        }

        private async Task<LandlordApplication?> GetLandlordApplicationSafeAsync(int id)
        {
            try
            {
                return await _context.LandlordApplications
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.Id == id);
            }
            catch (Exception ex) when (ex.Message.Contains("Invalid object name 'LandlordApplications'"))
            {
                return null;
            }
        }
    }
}
