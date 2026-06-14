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
            var adminIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(adminIdValue, out var adminId);

            // Chỉ đếm thống kê cho Dashboard
            ViewBag.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
            ViewBag.TotalAdmins = await _context.Users.CountAsync(u => u.Role == "Admin" && !u.IsDeleted);
            ViewBag.TotalLandlords = await _context.Users.CountAsync(u => u.Role == "Landlord" && !u.IsDeleted);
            ViewBag.TotalTenants = await _context.Users.CountAsync(u => u.Role == "User" && !u.IsDeleted);
            ViewBag.LockedUsers = await _context.Users.CountAsync(u => u.IsLocked && !u.IsDeleted);
            
            ViewBag.TotalRooms = await _context.Accommodations.CountAsync();
            ViewBag.PendingRoomsCount = await _context.Accommodations.CountAsync(a => !a.IsApproved && !a.IsRejected);
            ViewBag.ApprovedRooms = await _context.Accommodations.CountAsync(a => a.IsApproved);
            ViewBag.RejectedRooms = await _context.Accommodations.CountAsync(a => a.IsRejected);
            
            ViewBag.TotalRentalRequests = await _context.RentalRequests.CountAsync();

            // Thống kê hỗ trợ
            ViewBag.TotalSupportTickets = await _context.SupportTickets.CountAsync();
            ViewBag.PendingSupportTickets = await _context.SupportTickets.CountAsync(t => t.Status == "Pending");

            // Thống kê lịch sử xử lý
            ViewBag.TotalApprovalHistory = await _context.ApprovalHistories.CountAsync();

            var recentActivities = await _context.Accommodations
                .Include(a => a.Owner)
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToListAsync();
            ViewBag.RecentActivities = recentActivities;
            ViewBag.RecentNotifications = await _context.Notifications
                .Where(item => item.UserId == adminId && item.Type != "ChatMessage")
                .OrderByDescending(item => item.CreatedAt)
                .Take(5)
                .ToListAsync();
            ViewBag.UnreadNotifications = await _context.Notifications
                .CountAsync(item => item.UserId == adminId && item.Type != "ChatMessage" && !item.IsRead);

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ApprovalHistory()
        {
            var history = await _context.ApprovalHistories
                .OrderByDescending(h => h.ProcessedAt)
                .ToListAsync();
            return View(history);
        }

        [HttpGet]
        public async Task<IActionResult> PendingPosts()
        {
            var pendingRooms = await _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.City)
                .Include(a => a.District)
                .Where(a => !a.IsApproved && !a.IsRejected)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
            return View(pendingRooms);
        }

        [HttpGet]
        public async Task<IActionResult> ManagePosts(string type = "all", string status = "all")
        {
            ViewBag.CurrentType = type;
            ViewBag.CurrentStatus = status;

            var query = _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.City)
                .Include(a => a.District)
                .AsQueryable();

            if (type == "phongtro") query = query.Where(a => a.AccommodationType != "NhaNguyenCan");
            else if (type == "nhanguyencan") query = query.Where(a => a.AccommodationType == "NhaNguyenCan");

            if (status == "pending") query = query.Where(a => !a.IsApproved && !a.IsRejected);
            else if (status == "approved") query = query.Where(a => a.IsApproved);
            else if (status == "rejected") query = query.Where(a => a.IsRejected);
            else if (status == "hidden") query = query.Where(a => a.IsHidden);
            else if (status == "rentedout") query = query.Where(a => a.IsRentedOut);

            var rooms = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
            return View(rooms);
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
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsApproved && !a.IsRejected);
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
                    .Where(user => user.Role == "User" && !user.IsDeleted && !user.IsLocked)
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
            return RedirectToAction(nameof(PendingPosts));
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var room = await _context.Accommodations
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsApproved && !a.IsRejected);
            if (room != null)
            {
                room.IsRejected = true;
                room.RejectionReason = reason;

                var history = CreateHistory(room, "Rejected");
                _context.ApprovalHistories.Add(history);
                
                var ownerNotification = new AppNotification
                {
                    UserId = room.OwnerId,
                    Type = "Rejected",
                    Title = "Bài đăng đã bị từ chối",
                    Message = $"Lý do: {reason}",
                    Url = "/Accommodation/Index"
                };
                _context.Notifications.Add(ownerNotification);
                await _context.SaveChangesAsync();
                await SendNotification(ownerNotification);
                await NotifyProcessed(room, history, "Rejected", "Đã từ chối");
                
                TempData["Success"] = "Đã từ chối bài đăng!";
            }
            return RedirectToAction(nameof(PendingPosts));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleHidden(int id)
        {
            var room = await _context.Accommodations.FirstOrDefaultAsync(a => a.Id == id);
            if (room != null)
            {
                room.IsHidden = !room.IsHidden;
                await _context.SaveChangesAsync();
                TempData["Success"] = room.IsHidden ? "Đã ẩn bài đăng." : "Đã hiện bài đăng.";
            }
            return RedirectToAction(nameof(ManagePosts));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var room = await _context.Accommodations
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (room != null)
            {
                var storedDocuments = room.Documents?
                    .Where(item => !string.IsNullOrWhiteSpace(item.StoredFileName))
                    .Select(item => item.StoredFileName!)
                    .ToList() ?? new();
                
                _context.Accommodations.Remove(room);
                await _context.SaveChangesAsync();

                foreach (var storedFileName in storedDocuments)
                {
                    var filePath = Path.Combine(_environment.ContentRootPath, "App_Data", "land-documents", storedFileName);
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }
                TempData["Success"] = "Đã xóa bài đăng!";
            }
            return RedirectToAction(nameof(ManagePosts));
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
        public async Task<IActionResult> ManageUsers(string role = "all", string status = "all", string search = "")
        {
            ViewBag.CurrentRole = role;
            ViewBag.CurrentStatus = status;
            ViewBag.Search = search;

            var query = _context.Users
                .Where(u => !u.IsDeleted) // Không hiển thị người đã xóa mềm
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => (u.FullName != null && u.FullName.Contains(search)) || 
                                         (u.Email != null && u.Email.Contains(search)) ||
                                         (u.Phone != null && u.Phone.Contains(search)));
            }

            if (role != "all")
            {
                if (role == "admin") query = query.Where(u => u.Role == "Admin");
                else if (role == "landlord") query = query.Where(u => u.Role == "Landlord");
                else if (role == "tenant") query = query.Where(u => u.Role == "User");
            }

            if (status == "active") query = query.Where(u => !u.IsLocked);
            else if (status == "locked") query = query.Where(u => u.IsLocked);

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

            // Tính toán thêm số liệu (số bài đăng, liên hệ) có thể thực hiện trên View hoặc map vào ViewModel.
            // Để đơn giản ta dùng ViewBag hoặc nạp vào List. Ta sẽ map sang một danh sách ẩn danh/ViewModel nhỏ nếu cần.
            // Ở đây trả thẳng Users về View, View sẽ dùng Injection hoặc Count để đếm. Nhưng để tối ưu, ta tính luôn ở đây:
            var userIds = users.Select(u => u.Id).ToList();
            
            var roomCounts = await _context.Accommodations
                .Where(a => userIds.Contains(a.OwnerId))
                .GroupBy(a => a.OwnerId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), Approved = g.Count(a => a.IsApproved), Pending = g.Count(a => !a.IsApproved && !a.IsRejected), Rejected = g.Count(a => a.IsRejected) })
                .ToDictionaryAsync(x => x.UserId);
                
            var chatCounts = await _context.ChatConversations
                .Where(c => userIds.Contains(c.RenterId))
                .GroupBy(c => c.RenterId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId);

            ViewBag.RoomCounts = roomCounts;
            ViewBag.ChatCounts = chatCounts;

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> UserDetails(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            ViewBag.TotalRooms = await _context.Accommodations.CountAsync(a => a.OwnerId == id);
            ViewBag.ApprovedRooms = await _context.Accommodations.CountAsync(a => a.OwnerId == id && a.IsApproved);
            ViewBag.RejectedRooms = await _context.Accommodations.CountAsync(a => a.OwnerId == id && a.IsRejected);
            ViewBag.PendingRooms = await _context.Accommodations.CountAsync(a => a.OwnerId == id && !a.IsApproved && !a.IsRejected);
            
            ViewBag.RecentRooms = await _context.Accommodations
                .Where(a => a.OwnerId == id)
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.TotalChats = await _context.ChatConversations.CountAsync(c => c.RenterId == id);

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserLock(int id, string reason)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentAdminId)) return Unauthorized();

            if (id == currentAdminId)
            {
                TempData["Error"] = "Bạn không thể tự khóa tài khoản của mình.";
                return RedirectToAction(nameof(ManageUsers));
            }

            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsLocked = !user.IsLocked;
                if (user.IsLocked)
                {
                    user.LockReason = reason;
                    user.LockedAt = DateTime.Now;
                    user.LockedByAdminId = currentAdminId;
                }
                else
                {
                    user.LockReason = null;
                    user.LockedAt = null;
                    user.LockedByAdminId = null;
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = user.IsLocked ? $"Đã khóa tài khoản {user.FullName}" : $"Đã mở khóa tài khoản {user.FullName}";
            }
            return RedirectToAction(nameof(ManageUsers));
        }

        [HttpPost]
        public async Task<IActionResult> SoftDeleteUser(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int currentAdminId)) return Unauthorized();

            if (id == currentAdminId)
            {
                TempData["Error"] = "Bạn không thể tự xóa tài khoản của mình.";
                return RedirectToAction(nameof(ManageUsers));
            }

            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsDeleted = true;
                user.DeletedAt = DateTime.Now;
                user.DeletedByAdminId = currentAdminId;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa mềm tài khoản {user.FullName}.";
            }
            return RedirectToAction(nameof(ManageUsers));
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

        // [GET]: Hiển thị form đăng tin hộ cho Admin
        [HttpGet]
        public async Task<IActionResult> CreateRoom()
        {
            // Lấy danh sách Tỉnh/Thành phố từ Database để nạp vào thẻ Select (Combo box)
            ViewBag.Cities = await _context.Cities.ToListAsync();

            // Trả về giao diện CreateRoom.cshtml
            return View();
        }

        // [GET]: Hiển thị form chỉnh sửa tin cho Admin
        [HttpGet]
        public async Task<IActionResult> EditRoom(int id)
        {
            var room = await _context.Accommodations
                .Include(a => a.Images)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (room == null) return NotFound();

            // Tải danh sách Thành phố và Quận huyện tương ứng
            ViewBag.Cities = await _context.Cities.ToListAsync();
            ViewBag.Districts = await _context.Districts.Where(d => d.CityId == room.CityId).ToListAsync();

            return View(room);
        }
    }
}
