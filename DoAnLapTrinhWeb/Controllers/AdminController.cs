using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Data;
using System.Linq;
using System.Threading.Tasks;

namespace DoAnLapTrinhWeb.Controllers
{
    // [Unverified] Giả định tài khoản duyệt bài bắt buộc phải có Role là "Admin"
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
            // Lấy danh sách các bài ĐANG CHỜ DUYỆT (IsApproved = false)
            var pendingRooms = await _context.Accommodations
                .Include(a => a.Owner)
                .Where(a => a.IsApproved == false)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(pendingRooms);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var room = await _context.Accommodations.FindAsync(id);
            if (room != null)
            {
                room.IsApproved = true; // Đổi trạng thái thành Đã Duyệt
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
                _context.Accommodations.Remove(room); // Xóa bài nếu vi phạm
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa bài đăng!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}