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
            var room = await _context.Accommodations
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsApproved);
            if (room != null)
            {
                room.IsApproved = true; // Đổi trạng thái thành Đã Duyệt
                var history = CreateHistory(room, "Approved");
                _context.ApprovalHistories.Add(history);
                await _context.SaveChangesAsync();
                await NotifyProcessed(room, history, "Approved", "Đã duyệt");
                TempData["Success"] = "Đã duyệt bài đăng thành công!";
            }
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Ok();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
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
                _context.Accommodations.Remove(room); // Xóa bài nếu vi phạm
                await _context.SaveChangesAsync();
                foreach (var storedFileName in storedDocuments)
                {
                    var filePath = Path.Combine(_environment.ContentRootPath, "App_Data", "land-documents", storedFileName);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }
                await NotifyProcessed(room, history, "Rejected", "Đã từ chối");
                TempData["Success"] = "Đã xóa bài đăng!";
            }
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return Ok();
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
    }
}
