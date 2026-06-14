using System.Security.Claims;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Hubs;
using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<AccommodationHub> _hubContext;

        public NotificationController(AppDbContext context, IHubContext<AccommodationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Recent()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var items = await _context.Notifications
                .Where(item => item.UserId == userId && item.Type != "ChatMessage")
                .OrderByDescending(item => item.CreatedAt)
                .Take(30)
                .Select(item => new
                {
                    item.Id,
                    item.Title,
                    item.Message,
                    item.Url,
                    item.Type,
                    item.IsRead,
                    item.CreatedAt
                })
                .ToListAsync();

            return Json(items.Select(item => new
            {
                item.Id,
                item.Title,
                item.Message,
                item.Url,
                item.Type,
                item.IsRead,
                createdAt = item.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            }));
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var item = await _context.Notifications
                .FirstOrDefaultAsync(notification => notification.Id == id && notification.UserId == userId);
            if (item == null) return NotFound(new { success = false });

            if (!item.IsRead)
            {
                item.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var unreadItems = await _context.Notifications
                .Where(item => item.UserId == userId && item.Type != "ChatMessage" && !item.IsRead)
                .ToListAsync();

            foreach (var item in unreadItems) item.IsRead = true;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> RequestRent(int accommodationId, int occupants)
        {
            if (!TryGetUserId(out var renterId)) return Unauthorized();

            var room = await _context.Accommodations
                .Include(item => item.Owner)
                .FirstOrDefaultAsync(item => item.Id == accommodationId && item.IsApproved);
            if (room == null) return NotFound(new { message = "Phòng không tồn tại hoặc chưa được duyệt." });

            occupants = Math.Clamp(occupants, 1, room.MaxOccupants);
            var existingRequest = await _context.RentalRequests.AnyAsync(item =>
                item.AccommodationId == room.Id &&
                item.RenterId == renterId &&
                item.Status == "Pending");
            if (existingRequest) return BadRequest(new { message = "Bạn đã gửi yêu cầu thuê phòng này trước đó." });

            var renter = await _context.Users.FindAsync(renterId);
            var request = new RentalRequest
            {
                AccommodationId = room.Id,
                RenterId = renterId,
                Occupants = occupants
            };
            var ownerNotification = new AppNotification
            {
                UserId = room.OwnerId,
                Type = "RentalRequest",
                Title = $"Có yêu cầu thuê: {room.Title}",
                Message = $"{renter?.FullName ?? "Người thuê"} muốn thuê cho {occupants} người.",
                Url = "/RentalProcess/Index?status=Pending"
            };
            var renterNotification = new AppNotification
            {
                UserId = renterId,
                Type = "RentalRequestSent",
                Title = "Đã gửi yêu cầu thuê",
                Message = $"Yêu cầu thuê {room.Title} đã được gửi đến chủ trọ.",
                Url = $"/Accommodation/Details/{room.Id}"
            };

            _context.RentalRequests.Add(request);
            _context.Notifications.AddRange(ownerNotification, renterNotification);
            await _context.SaveChangesAsync();

            await SendNotification(room.OwnerId, ownerNotification);
            await SendNotification(renterId, renterNotification);
            return Ok(new { success = true, message = "Đã gửi yêu cầu thuê đến chủ trọ." });
        }

        private async Task SendNotification(int userId, AppNotification notification)
        {
            await _hubContext.Clients.Group($"User_{userId}").SendAsync("NotificationReceived", new
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

        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }
    }
}
