using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;

namespace DoAnLapTrinhWeb.Controllers
{
    // Bắt buộc người dùng phải đăng nhập mới được dùng các hàm trong Controller này
    [Authorize]
    public class FavoriteController : Controller
    {
        private readonly AppDbContext _context;

        public FavoriteController(AppDbContext context)
        {
            _context = context;
        }

        // --- 1. TRANG HIỂN THỊ DANH SÁCH YÊU THÍCH ---
        public async Task<IActionResult> Index()
        {
            var userId = await ResolveCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Truy vấn lấy danh sách các phòng trọ mà User này đã nhấn yêu thích
            var favoriteRooms = await _context.Favorites
                .Where(f => f.UserId == userId.Value)
                .Include(f => f.Accommodation)
                    .ThenInclude(a => a!.Images) // Lấy kèm hình ảnh
                .Select(f => f.Accommodation)
                .ToListAsync();

            return View(favoriteRooms);
        }

        // --- 2. API AJAX ĐỂ THÊM/XÓA YÊU THÍCH ---
        [HttpPost]
        public async Task<IActionResult> ToggleFavorite(int accommodationId)
        {
            var userId = await ResolveCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                // Trả về mã lỗi 401 (Unauthorized) nếu chưa đăng nhập
                return Unauthorized(new { message = "Vui lòng đăng nhập để thực hiện chức năng này." });
            }

            // Kiểm tra xem phòng này đã có trong danh sách yêu thích chưa
            var existingFavorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == userId.Value && f.AccommodationId == accommodationId);

            bool isFavorited;

            if (existingFavorite != null)
            {
                // Nếu đã có -> Xóa khỏi danh sách (Bỏ thích)
                _context.Favorites.Remove(existingFavorite);
                isFavorited = false;
            }
            else
            {
                // Nếu chưa có -> Thêm mới vào danh sách
                var newFavorite = new Favorite
                {
                    UserId = userId.Value,
                    AccommodationId = accommodationId
                };
                _context.Favorites.Add(newFavorite);
                isFavorited = true;
            }

            await _context.SaveChangesAsync();

            // Trả về JSON để Javascript cập nhật lại icon trái tim
            return Json(new { success = true, isFavorited = isFavorited });
        }

        private async Task<int?> ResolveCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out var userId))
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
