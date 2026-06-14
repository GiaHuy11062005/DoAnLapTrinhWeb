using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using DoAnLapTrinhWeb.Hubs;
using System.Security.Claims;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly IHubContext<AccommodationHub> _notificationHubContext;

        public ChatController(
            AppDbContext context,
            IHubContext<ChatHub> chatHubContext,
            IHubContext<AccommodationHub> notificationHubContext)
        {
            _context = context;
            _chatHubContext = chatHubContext;
            _notificationHubContext = notificationHubContext;
        }

        // =========================================================
        // HÀM TRỢ GIÚP LẤY ID NGƯỜI DÙNG HIỆN TẠI
        // =========================================================
        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out userId);
        }

        // =========================================================
        // GỬI THÔNG BÁO REALTIME QUA SIGNALR
        // =========================================================
        private async Task SendNotification(int userId, AppNotification notification)
        {
            await _notificationHubContext.Clients
                .Group($"User_{userId}")
                .SendAsync("NotificationReceived", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    message = notification.Message,
                    url = notification.Url,
                    type = notification.Type,
                    isRead = notification.IsRead,
                    createdAt = notification.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });
        }

        // =========================================================
        // ACTION STARTPOPUP: TẠO HOẶC MỞ CUỘC TRÒ CHUYỆN (AJAX)
        // URL: POST /Chat/StartPopup
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> StartPopup(int accommodationId)
        {
            if (!TryGetUserId(out int currentUserId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var accommodation = await _context.Accommodations
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(a => a.Id == accommodationId);

            if (accommodation == null)
                return NotFound(new { success = false, message = "Không tìm thấy phòng trọ." });

            // Nếu người dùng là chủ trọ của bài đăng này thì không cho phép tự chat
            if (accommodation.OwnerId == currentUserId)
                return BadRequest(new { success = false, message = "Bạn không thể tự nhắn tin cho chính mình." });

            // Kiểm tra conversation đã tồn tại chưa (tránh tạo trùng)
            var conversation = await _context.ChatConversations
                .FirstOrDefaultAsync(c =>
                    c.AccommodationId == accommodationId &&
                    c.RenterId == currentUserId &&
                    c.LandlordId == accommodation.OwnerId);

            bool isNewConversation = false;
            if (conversation == null)
            {
                conversation = new ChatConversation
                {
                    AccommodationId = accommodationId,
                    RenterId = currentUserId,
                    LandlordId = accommodation.OwnerId,
                    HasPendingNegotiation = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.ChatConversations.Add(conversation);
                isNewConversation = true;
            }

            // Kiểm tra RentalRequest
            var rentalRequest = await _context.RentalRequests
                .FirstOrDefaultAsync(r => r.AccommodationId == accommodationId && r.RenterId == currentUserId);

            if (rentalRequest == null)
            {
                rentalRequest = new RentalRequest
                {
                    AccommodationId = accommodationId,
                    RenterId = currentUserId,
                    Status = "Pending",
                    CreatedAt = DateTime.Now
                };
                _context.RentalRequests.Add(rentalRequest);
                if (!isNewConversation)
                {
                    conversation.HasPendingNegotiation = true;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, conversationId = conversation.Id });
        }

        // =========================================================
        // ACTION INBOX: DANH SÁCH CÁC CUỘC TRÒ CHUYỆN
        // URL: /Chat/Inbox
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Inbox(string timeFilter = "all", string typeFilter = "all", string statusFilter = "all", string keyword = "")
        {
            if (!TryGetUserId(out int currentUserId))
                return RedirectToAction("Login", "Auth");

            var role = User.FindFirstValue(ClaimTypes.Role);

            IQueryable<ChatConversation> query;

            if (role == "Landlord")
            {
                query = _context.ChatConversations
                    .Where(c => c.LandlordId == currentUserId);
            }
            else
            {
                query = _context.ChatConversations
                    .Where(c => c.RenterId == currentUserId);
            }

            // Lọc theo thời gian (dựa trên tin nhắn cuối cùng hoặc CreatedAt)
            if (timeFilter != "all")
            {
                DateTime startDate = DateTime.Today;
                if (timeFilter == "today") startDate = DateTime.Today;
                else if (timeFilter == "7days") startDate = DateTime.Today.AddDays(-7);
                else if (timeFilter == "30days") startDate = DateTime.Today.AddDays(-30);

                query = query.Where(c => c.UpdatedAt >= startDate);
            }

            // Lọc theo loại phòng
            if (typeFilter != "all")
            {
                query = query.Where(c => c.Accommodation!.AccommodationType == typeFilter);
            }

            // Lọc theo từ khóa (tên người gửi, tiêu đề phòng)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lowerKeyword = keyword.ToLower();
                if (role == "Landlord")
                {
                    query = query.Where(c => c.Renter!.FullName!.ToLower().Contains(lowerKeyword) || 
                                             c.Accommodation!.Title!.ToLower().Contains(lowerKeyword));
                }
                else
                {
                    query = query.Where(c => c.Landlord!.FullName!.ToLower().Contains(lowerKeyword) || 
                                             c.Accommodation!.Title!.ToLower().Contains(lowerKeyword));
                }
            }

            // Tải dữ liệu trước khi lọc trạng thái phức tạp
            var conversations = await query
                .Include(c => c.Accommodation)
                    .ThenInclude(a => a!.Images)
                .Include(c => c.Renter)
                .Include(c => c.Landlord)
                .Include(c => c.Messages)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            // Lọc theo trạng thái
            if (statusFilter != "all")
            {
                if (statusFilter == "unread")
                {
                    conversations = conversations.Where(c => c.Messages != null && c.Messages.Any(m => m.SenderId != currentUserId && !m.IsRead)).ToList();
                }
                else if (statusFilter == "negotiating")
                {
                    conversations = conversations.Where(c => c.HasPendingNegotiation).ToList();
                }
            }

            ViewBag.CurrentUserId = currentUserId;
            ViewBag.TimeFilter = timeFilter;
            ViewBag.TypeFilter = typeFilter;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.Keyword = keyword;

            return View(conversations);
        }

        // =========================================================
        // ACTION GETCHATHISTORY: LẤY LỊCH SỬ TIN NHẮN DẠNG PARTIAL VIEW
        // URL: GET /Chat/GetChatHistory?conversationId=...
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetChatHistory(int conversationId)
        {
            if (!TryGetUserId(out int currentUserId))
                return Unauthorized();

            var conversation = await _context.ChatConversations
                .Include(c => c.Accommodation)
                    .ThenInclude(a => a!.Images)
                .Include(c => c.Renter)
                .Include(c => c.Landlord)
                .Include(c => c.Messages!.OrderBy(m => m.CreatedAt))
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
                return NotFound();

            if (conversation.RenterId != currentUserId && conversation.LandlordId != currentUserId)
                return Forbid();

            // Đánh dấu tin nhắn của người kia là đã đọc
            var unreadMessages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId &&
                            m.SenderId != currentUserId &&
                            !m.IsRead)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var msg in unreadMessages)
                    msg.IsRead = true;
                await _context.SaveChangesAsync();
            }

            ViewBag.CurrentUserId = currentUserId;
            return PartialView("_ChatHistory", conversation);
        }

        // =========================================================
        // ACTION SENDMESSAGE: GỬI TIN NHẮN
        // URL: POST /Chat/SendMessage
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> SendMessage(int conversationId, string content)
        {
            if (!TryGetUserId(out int currentUserId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { success = false, message = "Nội dung tin nhắn không được để trống." });

            var conversation = await _context.ChatConversations
                .Include(c => c.Renter)
                .Include(c => c.Landlord)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
                return NotFound(new { success = false, message = "Cuộc trò chuyện không tồn tại." });

            if (conversation.RenterId != currentUserId && conversation.LandlordId != currentUserId)
                return Forbid();

            // Tạo tin nhắn mới
            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderId = currentUserId,
                Content = content.Trim(),
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.ChatMessages.Add(message);
            conversation.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Lấy tên người gửi
            var senderName = currentUserId == conversation.RenterId
                ? conversation.Renter?.FullName ?? "Người thuê"
                : conversation.Landlord?.FullName ?? "Chủ trọ";

            // Gửi tin nhắn realtime qua ChatHub
            await _chatHubContext.Clients
                .Group($"Chat_{conversationId}")
                .SendAsync("MessageReceived", new
                {
                    conversationId = conversationId,
                    senderId = currentUserId,
                    senderName = senderName,
                    content = message.Content,
                    createdAt = message.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });

            // Xác định người nhận (người còn lại trong conversation)
            var otherUserId = currentUserId == conversation.RenterId
                ? conversation.LandlordId
                : conversation.RenterId;

            // Tạo thông báo cho người nhận
            var truncatedContent = content.Length > 100 ? content.Substring(0, 100) + "..." : content;
            var notification = new AppNotification
            {
                UserId = otherUserId,
                Title = "Tin nhắn mới",
                Message = $"{senderName}: {truncatedContent}",
                Url = $"/Chat/Inbox?openChat={conversationId}",
                Type = "ChatMessage",
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Gửi thông báo realtime
            await SendNotification(otherUserId, notification);

            return Ok(new
            {
                success = true,
                senderId = currentUserId,
                senderName = senderName,
                content = message.Content,
                createdAt = message.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            });
        }

        // =========================================================
        // ACTION MARKASREAD: ĐÁNH DẤU ĐÃ ĐỌC
        // URL: POST /Chat/MarkAsRead
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int conversationId)
        {
            if (!TryGetUserId(out int currentUserId))
                return Unauthorized();

            var unreadMessages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId &&
                            m.SenderId != currentUserId &&
                            !m.IsRead)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var msg in unreadMessages)
                    msg.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }

        // =========================================================
        // ACTION ROOM: BACKWARD COMPATIBILITY CHO THÔNG BÁO CŨ
        // URL: /Chat/Room/{id}
        // =========================================================
        [HttpGet]
        public IActionResult Room(int id)
        {
            return RedirectToAction("Inbox", new { openChat = id });
        }

        // =========================================================
        // ACTION CONFIRM NEGOTIATION
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> ConfirmNegotiation(int conversationId)
        {
            if (!TryGetUserId(out int currentUserId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var conversation = await _context.ChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null || conversation.LandlordId != currentUserId)
                return NotFound(new { success = false, message = "Không tìm thấy hoặc không có quyền." });

            var rentalRequest = await _context.RentalRequests
                .FirstOrDefaultAsync(r => r.AccommodationId == conversation.AccommodationId && r.RenterId == conversation.RenterId);

            if (rentalRequest != null && rentalRequest.Status == "Pending")
            {
                rentalRequest.Status = "Negotiating";
                rentalRequest.ConfirmedAt = DateTime.Now;
                conversation.HasPendingNegotiation = false;
                await _context.SaveChangesAsync();

                // Notify UI via SignalR
                await _chatHubContext.Clients.Group($"Chat_{conversationId}")
                    .SendAsync("NegotiationConfirmed", conversationId);

                return Ok(new { success = true });
            }

            return BadRequest(new { success = false, message = "Không có yêu cầu nào đang chờ xác nhận." });
        }

        // =========================================================
        // ACTION REJECT NEGOTIATION
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> RejectNegotiation(int conversationId)
        {
            if (!TryGetUserId(out int currentUserId))
                return Unauthorized(new { success = false, message = "Chưa đăng nhập." });

            var conversation = await _context.ChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null || conversation.LandlordId != currentUserId)
                return NotFound(new { success = false, message = "Không tìm thấy hoặc không có quyền." });

            var rentalRequest = await _context.RentalRequests
                .FirstOrDefaultAsync(r => r.AccommodationId == conversation.AccommodationId && r.RenterId == conversation.RenterId);

            if (rentalRequest != null && rentalRequest.Status == "Pending")
            {
                rentalRequest.Status = "Rejected";
                conversation.HasPendingNegotiation = false;
                await _context.SaveChangesAsync();

                // Notify UI via SignalR
                await _chatHubContext.Clients.Group($"Chat_{conversationId}")
                    .SendAsync("NegotiationRejected", conversationId);

                return Ok(new { success = true });
            }

            return BadRequest(new { success = false, message = "Không có yêu cầu nào đang chờ xác nhận." });
        }
    }
}
