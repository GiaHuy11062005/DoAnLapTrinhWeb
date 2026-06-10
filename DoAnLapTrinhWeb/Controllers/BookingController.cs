using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize]
    public class BookingController : Controller
    {
        private readonly AppDbContext _context;

        public BookingController(AppDbContext context)
        {
            _context = context;
        }

        // Hàm phụ trợ: Lấy ID của người dùng hiện tại đang đăng nhập từ Session/Cookie
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        // Xử lý khi Sinh viên nhấn nút "Gửi yêu cầu" đặt phòng
        [HttpPost]
        public async Task<IActionResult> Create(int accommodationId, string message)
        {
            var studentId = GetCurrentUserId();
            var accommodation = await _context.Accommodations.FindAsync(accommodationId);

            if (accommodation == null) return NotFound();

            // Kiem tra xem da dat chua
            var existing = await _context.BookingRequests
                .FirstOrDefaultAsync(b => b.AccommodationId == accommodationId && b.StudentId == studentId && b.Status == "Pending");
            
            if (existing != null)
            {
                TempData["Error"] = "Bạn đã gửi yêu cầu đặt phòng này rồi, vui lòng chờ chủ trọ phản hồi.";
                return Redirect($"/Accommodation/Details/{accommodationId}");
            }

            var booking = new BookingRequest
            {
                AccommodationId = accommodationId,
                StudentId = studentId,
                LandlordId = accommodation.OwnerId,
                Message = message,
                Status = "Pending",
                CreatedAt = DateTime.Now
            };

            _context.BookingRequests.Add(booking);

            // Gui thong bao cho chu tro
            var student = await _context.Users.FindAsync(studentId);
            var notification = new Notification
            {
                UserId = accommodation.OwnerId,
                Title = "Có yêu cầu đặt phòng mới",
                Message = $"Sinh viên {student?.FullName} vừa gửi yêu cầu đặt phòng cho '{accommodation.Title}'.",
                ActionUrl = "/Booking/LandlordBookings",
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Gửi yêu cầu đặt phòng thành công! Hãy chờ chủ trọ liên hệ.";
            return Redirect($"/Accommodation/Details/{accommodationId}");
        }

        // Trang hiển thị danh sách các Đơn đặt phòng dành cho Chủ trọ (Chỉ Chủ trọ mới vào được)
        [Authorize(Roles = "Landlord")]
        [HttpGet]
        public async Task<IActionResult> LandlordBookings()
        {
            var landlordId = GetCurrentUserId();
            var bookings = await _context.BookingRequests
                .Include(b => b.Accommodation)
                .Include(b => b.Student)
                .Where(b => b.LandlordId == landlordId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        // Xử lý khi Chủ trọ nhấn nút "Chấp nhận" đơn đặt phòng
        [Authorize(Roles = "Landlord")]
        [HttpPost]
        public async Task<IActionResult> Accept(int id)
        {
            var landlordId = GetCurrentUserId();
            var booking = await _context.BookingRequests
                .Include(b => b.Accommodation)
                .Include(b => b.Landlord)
                .FirstOrDefaultAsync(b => b.Id == id && b.LandlordId == landlordId);

            if (booking == null) return NotFound();

            booking.Status = "Accepted";

            // Gui thong bao cho sinh vien
            var notification = new Notification
            {
                UserId = booking.StudentId,
                Title = "Yêu cầu đặt phòng được CHẤP NHẬN",
                Message = $"Chủ trọ {booking.Landlord?.FullName} đã chấp nhận yêu cầu của bạn cho phòng '{booking.Accommodation?.Title}'. Liên hệ SDT: {booking.Landlord?.Phone} để làm thủ tục.",
                ActionUrl = $"/Accommodation/Details/{booking.AccommodationId}",
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã chấp nhận yêu cầu đặt phòng.";
            return RedirectToAction(nameof(LandlordBookings));
        }

        // Xử lý khi Chủ trọ nhấn nút "Từ chối" đơn đặt phòng và nhập lý do
        [Authorize(Roles = "Landlord")]
        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var landlordId = GetCurrentUserId();
            var booking = await _context.BookingRequests
                .Include(b => b.Accommodation)
                .Include(b => b.Landlord)
                .FirstOrDefaultAsync(b => b.Id == id && b.LandlordId == landlordId);

            if (booking == null) return NotFound();

            booking.Status = "Rejected";

            // Gui thong bao cho sinh vien
            var notification = new Notification
            {
                UserId = booking.StudentId,
                Title = "Yêu cầu đặt phòng bị TỪ CHỐI",
                Message = $"Chủ trọ đã từ chối yêu cầu của bạn cho phòng '{booking.Accommodation?.Title}'. Lý do: {reason}",
                ActionUrl = $"/Accommodation/Details/{booking.AccommodationId}",
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã từ chối yêu cầu đặt phòng.";
            return RedirectToAction(nameof(LandlordBookings));
        }

        // Trang hiển thị lịch sử đặt phòng dành cho Sinh viên tự theo dõi trạng thái
        [HttpGet]
        public async Task<IActionResult> StudentBookings()
        {
            var studentId = GetCurrentUserId();
            var bookings = await _context.BookingRequests
                .Include(b => b.Accommodation)
                .Include(b => b.Landlord)
                .Where(b => b.StudentId == studentId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }
    }
}
