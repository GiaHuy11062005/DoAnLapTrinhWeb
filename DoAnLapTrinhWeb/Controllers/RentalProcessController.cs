using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using DoAnLapTrinhWeb.Hubs;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class RentalProcessController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<AccommodationHub> _accommodationHub;

        public RentalProcessController(AppDbContext context, IHubContext<AccommodationHub> accommodationHub)
        {
            _context = context;
            _accommodationHub = accommodationHub;
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out userId);
        }

        // URL: /RentalProcess/Index
        [HttpGet]
        public async Task<IActionResult> Index(string status = "All")
        {
            if (!TryGetUserId(out int landlordId)) return Unauthorized();

            var query = _context.RentalRequests
                .Include(r => r.Accommodation)
                .Include(r => r.Renter)
                .Where(r => r.Accommodation != null && r.Accommodation.OwnerId == landlordId);

            if (status != "All")
            {
                query = query.Where(r => r.Status == status);
            }

            var list = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            var conversationItems = await _context.ChatConversations
                .Where(item => item.LandlordId == landlordId)
                .Select(item => new { item.Id, item.AccommodationId, item.RenterId })
                .ToListAsync();
            var conversationLookup = conversationItems
                .GroupBy(item => $"{item.AccommodationId}:{item.RenterId}")
                .ToDictionary(group => group.Key, group => group.Max(item => item.Id));

            ViewBag.CurrentStatus = status;
            ViewBag.ConversationLookup = conversationLookup;
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> BeginNegotiation(int id)
        {
            if (!TryGetUserId(out int landlordId)) return Unauthorized();

            var request = await _context.RentalRequests
                .Include(item => item.Accommodation)
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.Status == "Pending" &&
                    item.Accommodation != null &&
                    item.Accommodation.OwnerId == landlordId);

            if (request == null)
                return BadRequest(new { success = false, message = "Yêu cầu không còn ở trạng thái chờ phản hồi." });
            var renterHasActiveRental = await _context.RentalRequests.AnyAsync(item =>
                item.RenterId == request.RenterId &&
                item.Status == "Success");
            if (renterHasActiveRental)
                return BadRequest(new { success = false, message = "Người thuê này đang có một hợp đồng thuê còn hiệu lực." });

            request.Status = "Negotiating";
            request.ConfirmedAt = DateTime.Now;
            var conversation = await _context.ChatConversations.FirstOrDefaultAsync(item =>
                item.AccommodationId == request.AccommodationId &&
                item.RenterId == request.RenterId &&
                item.LandlordId == landlordId);
            if (conversation == null)
            {
                conversation = new ChatConversation
                {
                    AccommodationId = request.AccommodationId,
                    RenterId = request.RenterId,
                    LandlordId = landlordId,
                    HasPendingNegotiation = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.ChatConversations.Add(conversation);
            }
            else
            {
                conversation.HasPendingNegotiation = false;
                conversation.UpdatedAt = DateTime.Now;
            }

            var notification = CreateRenterNotification(
                request,
                "Chủ trọ đã phản hồi yêu cầu",
                $"Yêu cầu thuê {request.Accommodation?.Title} đã chuyển sang thương lượng.");
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            await SendNotification(notification);
            return Ok(new { success = true, conversationId = conversation.Id });
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            if (!TryGetUserId(out int landlordId)) return Unauthorized();

            var request = await _context.RentalRequests
                .Include(item => item.Accommodation)
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.Status == "Pending" &&
                    item.Accommodation != null &&
                    item.Accommodation.OwnerId == landlordId);

            if (request == null)
                return BadRequest(new { success = false, message = "Yêu cầu không còn ở trạng thái chờ phản hồi." });

            request.Status = "Rejected";
            var conversation = await _context.ChatConversations.FirstOrDefaultAsync(item =>
                item.AccommodationId == request.AccommodationId &&
                item.RenterId == request.RenterId &&
                item.LandlordId == landlordId);
            if (conversation != null) conversation.HasPendingNegotiation = false;

            var notification = CreateRenterNotification(
                request,
                "Yêu cầu thuê chưa được chấp nhận",
                $"Chủ trọ chưa thể tiếp nhận yêu cầu thuê {request.Accommodation?.Title}.");
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            await SendNotification(notification);
            return Ok(new { success = true });
        }

        // URL: POST /RentalProcess/Success
        [HttpPost]
        public async Task<IActionResult> Success(int id)
        {
            if (!TryGetUserId(out int landlordId)) return Unauthorized();

            var request = await _context.RentalRequests
                .Include(r => r.Accommodation)
                .FirstOrDefaultAsync(r => r.Id == id && r.Accommodation != null && r.Accommodation.OwnerId == landlordId);

            if (request == null || request.Status != "Negotiating")
                return BadRequest(new { success = false, message = "Không tìm thấy yêu cầu hoặc trạng thái không hợp lệ." });
            var renterAlreadyHasActiveRental = await _context.RentalRequests.AnyAsync(item =>
                item.RenterId == request.RenterId &&
                item.Id != request.Id &&
                item.Status == "Success");
            if (renterAlreadyHasActiveRental)
                return BadRequest(new { success = false, message = "Người thuê này đang có một hợp đồng thuê khác còn hiệu lực." });

            // 1. Đổi trạng thái request
            request.Status = "Success";
            request.ConfirmedAt = DateTime.Now;

            // 2. Xử lý Accommodation logic
            var acc = request.Accommodation!;
            var isWholeAccommodation = acc.AccommodationType == "NhaNguyenCan" || acc.AccommodationType == "CanHo";
            if (acc.IsRentedOut || (isWholeAccommodation ? acc.IsRented : acc.AvailableRooms <= 0))
                return BadRequest(new { success = false, message = "Phòng này hiện không còn chỗ trống để xác nhận cho thuê." });
            if (isWholeAccommodation)
            {
                acc.IsRentedOut = true;
                acc.IsRented = true;
            }
            else
            {
                acc.AvailableRooms -= 1;
                if (acc.AvailableRooms <= 0)
                {
                    acc.AvailableRooms = 0;
                    acc.IsRentedOut = true;
                }
            }

            var notification = CreateRenterNotification(
                request,
                "Thuê thành công - hãy đánh giá phòng",
                $"{request.Accommodation?.Title} đã được xác nhận cho thuê thành công. Hãy chia sẻ đánh giá để giúp người thuê sau chọn phòng tốt hơn.",
                $"/Accommodation/Details/{request.AccommodationId}#roomReviewSection");
            notification.Type = "ReviewRequest";
            _context.Notifications.Add(notification);

            var otherOpenRequests = await _context.RentalRequests
                .Where(item =>
                    item.RenterId == request.RenterId &&
                    item.Id != request.Id &&
                    (item.Status == "Pending" || item.Status == "Negotiating"))
                .ToListAsync();
            foreach (var openRequest in otherOpenRequests)
                openRequest.Status = "Cancelled";
            if (otherOpenRequests.Count > 0)
            {
                var otherConversations = await _context.ChatConversations
                    .Where(item =>
                        item.RenterId == request.RenterId &&
                        item.AccommodationId != request.AccommodationId &&
                        item.HasPendingNegotiation)
                    .ToListAsync();
                foreach (var conversation in otherConversations)
                    conversation.HasPendingNegotiation = false;
            }

            await _context.SaveChangesAsync();

            // 3. Notify real-time (disable buttons for this accommodation)
            await _accommodationHub.Clients.All.SendAsync("AccommodationStatusChanged", new { id = acc.Id, isRentedOut = acc.IsRentedOut });
            await SendNotification(notification);

            return Ok(new { success = true });
        }

        // URL: POST /RentalProcess/Cancel
        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            if (!TryGetUserId(out int landlordId)) return Unauthorized();

            var request = await _context.RentalRequests
                .Include(r => r.Accommodation)
                .FirstOrDefaultAsync(r => r.Id == id && r.Accommodation != null && r.Accommodation.OwnerId == landlordId);

            if (request == null || request.Status != "Negotiating")
                return BadRequest(new { success = false, message = "Không tìm thấy yêu cầu hoặc trạng thái không hợp lệ." });

            // 1. Đổi trạng thái request
            request.Status = "Cancelled";
            // Không trừ phòng, không đổi trạng thái bài đăng

            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> EndContract(int id)
        {
            if (!TryGetUserId(out int landlordId)) return Unauthorized();

            var request = await _context.RentalRequests
                .Include(item => item.Accommodation)
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.Status == "Success" &&
                    item.Accommodation != null &&
                    item.Accommodation.OwnerId == landlordId);
            if (request == null)
                return BadRequest(new { success = false, message = "Không tìm thấy hợp đồng đang hoạt động." });

            request.Status = "Completed";
            var accommodation = request.Accommodation!;
            if (accommodation.AccommodationType == "NhaNguyenCan" || accommodation.AccommodationType == "CanHo")
            {
                accommodation.IsRented = false;
                accommodation.IsRentedOut = false;
            }
            else
            {
                accommodation.AvailableRooms += 1;
                accommodation.IsRentedOut = false;
            }

            var notification = CreateRenterNotification(
                request,
                "Hợp đồng thuê đã kết thúc",
                $"Hợp đồng thuê {accommodation.Title} đã được chủ trọ xác nhận kết thúc.",
                "/Rental/MyRentals");
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            await _accommodationHub.Clients.All.SendAsync("AccommodationStatusChanged", new
            {
                id = accommodation.Id,
                isRentedOut = accommodation.IsRentedOut
            });
            await SendNotification(notification);
            return Ok(new { success = true });
        }

        private static AppNotification CreateRenterNotification(RentalRequest request, string title, string message, string? url = null)
        {
            return new AppNotification
            {
                UserId = request.RenterId,
                Type = "RentalRequestSent",
                Title = title,
                Message = message,
                Url = url ?? $"/Accommodation/Details/{request.AccommodationId}"
            };
        }

        private async Task SendNotification(AppNotification notification)
        {
            await _accommodationHub.Clients.Group($"User_{notification.UserId}").SendAsync("NotificationReceived", new
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
    }
}
