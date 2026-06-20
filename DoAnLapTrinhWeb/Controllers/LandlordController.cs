using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class LandlordController : Controller
    {
        private readonly AppDbContext _context;

        public LandlordController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard(string timeFilter = "all", string typeFilter = "all")
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId)) return Unauthorized();

            ViewBag.CurrentTimeFilter = timeFilter;
            ViewBag.CurrentTypeFilter = typeFilter;

            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";

            // 1. Lọc theo thời gian
            DateTime? startDate = null;
            if (timeFilter == "today") startDate = DateTime.Today;
            else if (timeFilter == "7days") startDate = DateTime.Today.AddDays(-7);
            else if (timeFilter == "30days") startDate = DateTime.Today.AddDays(-30);

            // 2. Query cơ sở Bài Đăng
            var queryAcc = _context.Accommodations.Where(a => a.OwnerId == userId);
            if (startDate.HasValue) queryAcc = queryAcc.Where(a => a.CreatedAt >= startDate.Value);
            if (typeFilter != "all") queryAcc = queryAcc.Where(a => a.AccommodationType == typeFilter);

            var totalPostsCount = await queryAcc.CountAsync();
            var pendingPostsCount = await queryAcc.CountAsync(a => !a.IsApproved);
            var approvedPostsCount = await queryAcc.CountAsync(a => a.IsApproved);
            var rentedOutCount = await queryAcc.CountAsync(a => a.IsRentedOut || (a.AccommodationType == "NhaNguyenCan" && a.IsRented));
            
            // Tính số bài bị từ chối (dựa trên email chủ trọ trong ApprovalHistory)
            var rejectedQuery = _context.ApprovalHistories.Where(h => h.OwnerEmail == userEmail && h.Action == "Rejected");
            if (startDate.HasValue) rejectedQuery = rejectedQuery.Where(h => h.ProcessedAt >= startDate.Value);
            var rejectedPostsCount = await rejectedQuery.CountAsync();

            ViewBag.TotalPosts = totalPostsCount;
            ViewBag.PendingPosts = pendingPostsCount;
            ViewBag.ApprovedPosts = approvedPostsCount;
            ViewBag.RejectedPosts = rejectedPostsCount;
            ViewBag.RentedOutPosts = rentedOutCount;

            // 3. Tình trạng phòng trống (Không tính nhà nguyên căn cho số phòng)
            var roomQuery = queryAcc.Where(a => a.AccommodationType != "NhaNguyenCan");
            ViewBag.TotalAvailableRooms = await roomQuery.SumAsync(a => a.AvailableRooms);

            var houseQuery = queryAcc.Where(a => a.AccommodationType == "NhaNguyenCan");
            ViewBag.TotalHouses = await houseQuery.CountAsync();
            ViewBag.RentedHouses = await houseQuery.CountAsync(a => a.IsRented);

            // 4. Query Yêu Cầu Thuê
            var reqQuery = _context.RentalRequests.Include(r => r.Accommodation).Where(r => r.Accommodation!.OwnerId == userId);
            if (startDate.HasValue) reqQuery = reqQuery.Where(r => r.CreatedAt >= startDate.Value);
            if (typeFilter != "all") reqQuery = reqQuery.Where(r => r.Accommodation!.AccommodationType == typeFilter);

            ViewBag.TotalRequests = await reqQuery.CountAsync();
            ViewBag.PendingRequests = await reqQuery.CountAsync(r => r.Status == "Pending");
            ViewBag.NegotiatingRequests = await reqQuery.CountAsync(r => r.Status == "Negotiating");
            ViewBag.SuccessRequests = await reqQuery.CountAsync(r => r.Status == "Success");
            ViewBag.CancelledRequests = await reqQuery.CountAsync(r => r.Status == "Cancelled" || r.Status == "Rejected");
            
            var today = DateTime.Today;
            ViewBag.NewRequestsToday = await reqQuery.CountAsync(r => r.CreatedAt >= today);

            // Chart data
            var chartRequests = await _context.RentalRequests
                .Where(r => r.Accommodation!.OwnerId == userId && r.CreatedAt >= DateTime.Today.AddDays(-6))
                .GroupBy(r => r.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.ChartRequestDates = string.Join(",", chartRequests.Select(x => x.Date.ToString("dd/MM")));
            ViewBag.ChartRequestCounts = string.Join(",", chartRequests.Select(x => x.Count));

            // 5. Bảng Bài Đăng Gần Đây (Take 5)
            ViewBag.RecentPosts = await queryAcc
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToListAsync();

            // 6. Bảng Yêu Cầu Thuê Gần Đây (Take 5)
            ViewBag.RecentRequests = await reqQuery
                .Include(r => r.Renter)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToListAsync();

            // 7. Tin nhắn
            ViewBag.TotalConversations = await _context.ChatConversations.CountAsync(c => c.LandlordId == userId);
            ViewBag.UnreadMessages = await _context.ChatMessages.CountAsync(m => m.Conversation!.LandlordId == userId && m.SenderId != userId && !m.IsRead);
            ViewBag.RecentNotifications = await _context.Notifications
                .Where(item => item.UserId == userId && item.Type != "ChatMessage" && item.Type != "NewAccommodation")
                .OrderByDescending(item => item.CreatedAt)
                .Take(5)
                .ToListAsync();
            ViewBag.UnreadNotifications = await _context.Notifications
                .CountAsync(item => item.UserId == userId && item.Type != "ChatMessage" && item.Type != "NewAccommodation" && !item.IsRead);

            return View();
        }
    }
}
