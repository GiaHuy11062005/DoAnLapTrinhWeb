using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Bắt buộc phải có để dùng Include, ToListAsync
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims; // Để lấy thông tin đăng nhập

namespace DoAnLapTrinhWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        // Đổi thành async Task<IActionResult> để tận dụng xử lý bất đồng bộ
        public async Task<IActionResult> Index()
        {
            // 1. Load danh sách thành phố cho thanh tìm kiếm ở Banner trang chủ
            ViewBag.Cities = await _context.Cities.ToListAsync();

            // 1a. Tỉnh/thành nổi bật có phòng đang được duyệt
            ViewBag.FeaturedCities = await _context.Cities
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.GoogleMapsUrl,
                    RoomCount = _context.Accommodations.Count(a => a.CityId == c.Id && a.IsApproved)
                })
                .Where(x => x.RoomCount > 0)
                .OrderByDescending(x => x.RoomCount)
                .ThenBy(x => x.Name)
                .Take(3)
                .ToListAsync();

            // 2. LOGIC ĐỀ XUẤT TRỌ GẦN NHẤT DÀNH CHO USER ĐÃ ĐĂNG NHẬP
            List<Accommodation> suggestedRooms = new List<Accommodation>();

            // Kiểm tra xem trình duyệt đã có session/cookie đăng nhập (người dùng đã Login) chưa
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                // Lấy ID của User đang đăng nhập (Từ cấu trúc Claims bảo mật của ASP.NET)
                var userId = await ResolveCurrentUserIdAsync();
                if (userId.HasValue)
                {
                    // Lấy thông tin địa chỉ của User này
                    var currentUser = await _context.Users.FindAsync(userId.Value);

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

            // Truyền danh sách đề xuất ra View (nếu chưa đăng nhập thì list này sẽ rỗng)
            ViewBag.SuggestedRooms = suggestedRooms;

            // 3. Trọ nổi bật dùng cho user bình thường ngay cả khi chưa đăng nhập (Lấy 6 phòng mới nhất đã duyệt)
            ViewBag.FeaturedRooms = await _context.Accommodations
                .Include(a => a.Images)
                .Include(a => a.City)
                .Include(a => a.District)
                .Where(a => a.IsApproved)
                .OrderByDescending(a => a.CreatedAt)
                .Take(6)
                .ToListAsync();

            ViewBag.RoomsNearSchools = await _context.Accommodations
                .Include(a => a.Images)
                .Include(a => a.City)
                .Include(a => a.District)
                .Where(a => a.IsApproved && a.Title != null && (
                    a.Title.Contains("HCMUT") ||
                    a.Title.Contains("UEL") ||
                    a.Title.Contains("Nông Lâm") ||
                    a.Title.Contains("HUTECH") ||
                    a.Title.Contains("FPT")))
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View();
        }

        // Hàm gọi qua API (AJAX) để lấy danh sách Quận/Huyện có phòng trọ (Lọc khi chọn Thành phố)
        [HttpGet]
        public IActionResult GetDistrictsWithRooms(int cityId)
        {
            var districts = _context.Districts
                .Where(d => d.CityId == cityId &&
                            _context.Accommodations.Any(a => a.DistrictId == d.Id && a.IsApproved == true))
                .Select(d => new { id = d.Id, name = d.Name })
                .ToList();

            return Json(districts);
        }

        // Hàm phụ trợ giúp lấy ID của user đăng nhập
        private async Task<int?> ResolveCurrentUserIdAsync()
        {
            // Thử lấy ID thẳng từ Claim (Nếu login bình thường)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            return currentUser?.Id;
        }
    }
}
