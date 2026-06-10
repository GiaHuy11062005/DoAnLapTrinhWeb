using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Bắt buộc phải có để dùng Include, ToListAsync
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims; // Để lấy thông tin đăng nhập

using Microsoft.AspNetCore.Authorization;

namespace DoAnLapTrinhWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        // Đổi thành async Task<IActionResult>
        public async Task<IActionResult> Index()
        {
            // 1. Load danh sách thành phố cho thanh tìm kiếm
            ViewBag.Cities = await _context.Cities.ToListAsync();

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
                            .Where(a => a.IsApproved == true && a.CityId == currentUser.CityId)
                            // THUẬT TOÁN ĐỀ XUẤT: Cùng Quận/Huyện với User thì đẩy lên đầu (true > false)
                            .OrderByDescending(a => a.DistrictId == currentUser.DistrictId)
                            .ThenByDescending(a => a.CreatedAt) // Sau đó ưu tiên phòng mới đăng
                            .Take(6) // Lấy tối đa 6 phòng để không làm nặng trang chủ
                            .ToListAsync();
                    }
                }
            }

            // Khách chưa đăng nhập hoặc chưa có địa chỉ vẫn thấy phòng nổi bật mới nhất.
            if (suggestedRooms.Count == 0)
            {
                suggestedRooms = await _context.Accommodations
                    .Include(a => a.Images)
                    .Include(a => a.City)
                    .Include(a => a.District)
                    .Where(a => a.IsApproved)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(6)
                    .ToListAsync();
            }
            else
            {
                var suggestedIds = suggestedRooms.Select(room => room.Id).ToList();
                suggestedRooms = await _context.Accommodations
                    .Include(a => a.Images)
                    .Include(a => a.City)
                    .Include(a => a.District)
                    .Where(a => suggestedIds.Contains(a.Id))
                    .ToListAsync();
            }

            // Truyền danh sách đề xuất ra View
            ViewBag.SuggestedRooms = suggestedRooms;

            return View();
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
    }
}
