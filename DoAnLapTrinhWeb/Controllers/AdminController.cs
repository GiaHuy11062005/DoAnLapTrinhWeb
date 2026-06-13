using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Data;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Security.Claims;
using DoAnLapTrinhWeb.Models;
using DoAnLapTrinhWeb.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace DoAnLapTrinhWeb.Controllers
{
    // [Unverified] Giả định tài khoản duyệt bài bắt buộc phải có Role là "Admin"
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<AccommodationHub> _hubContext;
        private readonly IWebHostEnvironment _environment;

        public AdminController(AppDbContext context, IHubContext<AccommodationHub> hubContext, IWebHostEnvironment environment)
        {
            _context = context;
            _hubContext = hubContext;
            _environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy danh sách các bài ĐANG CHỜ DUYỆT (IsApproved = false)
            var pendingRooms = await _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.City)
                .Include(a => a.District)
                .Include(a => a.Documents)
                .Where(a => a.IsApproved == false)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewBag.ApprovalHistory = await _context.ApprovalHistories
                .OrderByDescending(history => history.ProcessedAt)
                .Take(100)
                .ToListAsync();

            return View(pendingRooms);
        }

        [HttpGet]
        public async Task<IActionResult> PendingRoom(int id)
        {
            var room = await _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.City)
                .Include(a => a.District)
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsApproved);

            if (room == null) return NotFound();

            return Json(new
            {
                room.Id,
                createdAt = room.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                ownerName = room.Owner?.FullName ?? "Không rõ",
                ownerEmail = room.Owner?.Email ?? "",
                room.Title,
                location = $"{room.District?.Name}, {room.City?.Name}",
                price = room.Price.ToString("N0")
            });
        }

        [HttpGet]
        public async Task<IActionResult> Documents(int id)
        {
            var room = await _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (room == null) return NotFound();
            return View(room);
        }

        [HttpGet]
        public async Task<IActionResult> ViewDocument(int id)
        {
            var document = await _context.AccommodationDocuments.FirstOrDefaultAsync(item => item.Id == id);
            if (document == null || string.IsNullOrWhiteSpace(document.StoredFileName)) return NotFound();

            var safeStoredFileName = Path.GetFileName(document.StoredFileName);
            var filePath = Path.Combine(_environment.ContentRootPath, "App_Data", "land-documents", safeStoredFileName);
            if (!System.IO.File.Exists(filePath)) return NotFound();
            return PhysicalFile(filePath, document.ContentType ?? "application/octet-stream", enableRangeProcessing: true);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var approved = false;
            var room = await _context.Accommodations
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsApproved);
            if (room != null)
            {
                room.IsApproved = true; // Đổi trạng thái thành Đã Duyệt
                var history = CreateHistory(room, "Approved");
                _context.ApprovalHistories.Add(history);
                var ownerNotification = new AppNotification
                {
                    UserId = room.OwnerId,
                    Type = "Approval",
                    Title = "Bài đăng đã được duyệt",
                    Message = room.Title ?? "Bài đăng phòng trọ",
                    Url = $"/Accommodation/Details/{room.Id}"
                };
                _context.Notifications.Add(ownerNotification);

                var renterIds = await _context.Users
                    .Where(user => user.Role == "User")
                    .Select(user => user.Id)
                    .ToListAsync();
                var renterNotifications = renterIds.Select(renterId => new AppNotification
                {
                    UserId = renterId,
                    Type = "NewAccommodation",
                    Title = "Có phòng trọ mới vừa được duyệt",
                    Message = room.Title ?? "Xem phòng trọ mới",
                    Url = $"/Accommodation/Details/{room.Id}"
                }).ToList();
                _context.Notifications.AddRange(renterNotifications);
                await _context.SaveChangesAsync();
                await SendNotification(ownerNotification);
                foreach (var notification in renterNotifications) await SendNotification(notification);
                await NotifyProcessed(room, history, "Approved", "Đã duyệt");
                approved = true;
            }
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Ok();
            if (approved) TempData["Success"] = "Đã duyệt bài đăng thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = false;
            var room = await _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsApproved);
            if (room != null)
            {
                var storedDocuments = room.Documents?
                    .Where(item => !string.IsNullOrWhiteSpace(item.StoredFileName))
                    .Select(item => item.StoredFileName!)
                    .ToList() ?? new();
                var history = CreateHistory(room, "Rejected");
                _context.ApprovalHistories.Add(history);
                var ownerNotification = new AppNotification
                {
                    UserId = room.OwnerId,
                    Type = "Rejected",
                    Title = "Bài đăng đã bị từ chối",
                    Message = room.Title ?? "Bài đăng phòng trọ",
                    Url = "/Accommodation/Index"
                };
                _context.Notifications.Add(ownerNotification);
                _context.Accommodations.Remove(room); // Xóa bài nếu vi phạm
                await _context.SaveChangesAsync();
                await SendNotification(ownerNotification);
                foreach (var storedFileName in storedDocuments)
                {
                    var filePath = Path.Combine(_environment.ContentRootPath, "App_Data", "land-documents", storedFileName);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }
                await NotifyProcessed(room, history, "Rejected", "Đã từ chối");
                deleted = true;
            }
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Ok();
            if (deleted) TempData["Success"] = "Đã xóa bài đăng!";
            return RedirectToAction(nameof(Index));
        }

        private async Task NotifyProcessed(Accommodation room, ApprovalHistory history, string status, string statusLabel)
        {
            var payload = new
            {
                id = room.Id,
                status,
                statusLabel,
                historyId = history.Id,
                title = history.AccommodationTitle,
                ownerName = history.OwnerName,
                ownerEmail = history.OwnerEmail,
                price = history.Price.ToString("N0"),
                processedBy = history.ProcessedBy,
                processedAt = history.ProcessedAt.ToString("dd/MM/yyyy HH:mm")
            };
            await _hubContext.Clients.Group($"User_{room.OwnerId}").SendAsync("AccommodationStatusChanged", payload);
            await _hubContext.Clients.Group("Admins").SendAsync("PendingAccommodationProcessed", payload);
        }

        private ApprovalHistory CreateHistory(Accommodation room, string action)
        {
            return new ApprovalHistory
            {
                AccommodationId = room.Id,
                AccommodationTitle = room.Title,
                OwnerName = room.Owner?.FullName,
                OwnerEmail = room.Owner?.Email,
                Price = room.Price,
                Action = action,
                ProcessedBy = User.FindFirstValue(ClaimTypes.Name) ?? "Admin",
                ProcessedAt = DateTime.Now
            };
        }

        private async Task SendNotification(AppNotification notification)
        {
            await _hubContext.Clients.Group($"User_{notification.UserId}").SendAsync("NotificationReceived", new
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

        [HttpGet]
        public async Task<IActionResult> SupportTickets()
        {
            var tickets = await _context.SupportTickets
                .OrderBy(t => t.Status == "Pending" ? 0 : 1) // Pending lên đầu
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        [HttpGet]
        public async Task<IActionResult> TicketDetails(int id)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [HttpPost]
        public async Task<IActionResult> ResolveTicket(int id, string actionType, string? responseMessage)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket != null && ticket.Status == "Pending")
            {
                if (actionType == "Reject")
                {
                    ticket.Status = "Rejected";
                }
                else
                {
                    ticket.Status = "Resolved";
                }
                
                ticket.AdminResponse = responseMessage;
                
                // Nếu người gửi có tài khoản, báo cho họ biết
                if (ticket.UserId.HasValue)
                {
                    string defaultMsg = ticket.Status == "Resolved" 
                        ? $"Admin đã chấp nhận và xử lý yêu cầu: {ticket.Subject}" 
                        : $"Admin đã từ chối yêu cầu: {ticket.Subject}";

                    var msg = string.IsNullOrWhiteSpace(responseMessage) 
                        ? defaultMsg 
                        : $"Phản hồi từ Admin: {responseMessage}";

                    var userNotification = new AppNotification
                    {
                        UserId = ticket.UserId.Value,
                        Type = "SupportTicket",
                        Title = ticket.Status == "Resolved" ? "Hỗ trợ đã được giải quyết" : "Hỗ trợ bị từ chối",
                        Message = msg,
                        Url = "#"
                    };
                    _context.Notifications.Add(userNotification);
                    await _context.SaveChangesAsync();
                    await SendNotification(userNotification);
                }
                else
                {
                    await _context.SaveChangesAsync();
                }
                
                TempData["Success"] = ticket.Status == "Resolved" 
                    ? "Đã đánh dấu giải quyết thành công!" 
                    : "Đã từ chối yêu cầu hỗ trợ!";
            }
            return RedirectToAction(nameof(SupportTickets));
        }
    }
}
