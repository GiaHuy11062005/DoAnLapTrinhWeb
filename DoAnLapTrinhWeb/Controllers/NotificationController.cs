using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DoAnLapTrinhWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly AppDbContext _context;

        public NotificationController(AppDbContext context)
        {
            _context = context;
        }

        // Lấy ID người dùng đang đăng nhập
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        // Trả về số lượng thông báo CHƯA ĐỌC (dùng cho AJAX để hiện số lên cái chuông)
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Json(new { count = 0 });

            var count = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();

            return Json(new { count });
        }

        // Trả về danh sách 10 thông báo mới nhất (dùng cho AJAX đổ dữ liệu vào menu thả xuống của chuông)
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .Select(n => new {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.IsRead,
                    n.ActionUrl,
                    CreatedAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return Json(notifications);
        }

        // Đánh dấu 1 thông báo là ĐÃ ĐỌC khi người dùng click vào nó, sau đó chuyển hướng đến trang liên quan
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = GetCurrentUserId();
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
                
                if (!string.IsNullOrEmpty(notification.ActionUrl))
                {
                    return Redirect(notification.ActionUrl);
                }
            }

            return RedirectToAction("Index", "Home");
        }
        
        // Nút "Đánh dấu tất cả đã đọc" trong menu thông báo
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            var unreadNotifs = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            
            foreach (var n in unreadNotifs)
            {
                n.IsRead = true;
            }
            
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
