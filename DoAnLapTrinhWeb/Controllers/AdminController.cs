using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using DoAnLapTrinhWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DoAnLapTrinhWeb.Controllers
{
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
            var pendingRooms = await _context.Accommodations
                .Include(a => a.Owner)
                .Include(a => a.Images)
                .Where(a => a.IsApproved == false)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var pendingApplications = await GetPendingApplicationsSafeAsync();

            return View(new AdminDashboardViewModel
            {
                PendingRooms = pendingRooms,
                PendingLandlordApplications = pendingApplications
            });
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var room = await _context.Accommodations.FindAsync(id);
            if (room != null)
            {
                room.IsApproved = true;
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
                _context.Accommodations.Remove(room);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa bài đăng!";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> LandlordApplicationDetails(int id)
        {
            var application = await GetLandlordApplicationSafeAsync(id);

            if (application == null)
            {
                return NotFound();
            }

            return View(application);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveLandlordApplication(int id)
        {
            var application = await GetLandlordApplicationSafeAsync(id);

            if (application == null)
            {
                return NotFound();
            }

            application.Status = "Approved";
            application.ReviewedAt = DateTime.Now;
            application.ReviewedByAdminId = GetCurrentAdminId();
            application.RejectionReason = null;

            if (application.User != null)
            {
                application.User.Role = "Landlord";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã duyệt yêu cầu đăng ký làm chủ trọ.";
            return RedirectToAction(nameof(LandlordApplicationDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RejectLandlordApplication(int id, string? rejectionReason)
        {
            var application = await GetLandlordApplicationSafeAsync(id);

            if (application == null)
            {
                return NotFound();
            }

            application.Status = "Rejected";
            application.ReviewedAt = DateTime.Now;
            application.ReviewedByAdminId = GetCurrentAdminId();
            application.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason)
                ? "Không đạt yêu cầu."
                : rejectionReason.Trim();

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã từ chối yêu cầu đăng ký làm chủ trọ.";
            return RedirectToAction(nameof(LandlordApplicationDetails), new { id });
        }

        private int? GetCurrentAdminId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private async Task<List<LandlordApplication>> GetPendingApplicationsSafeAsync()
        {
            try
            {
                return await _context.LandlordApplications
                    .Include(a => a.User)
                    .Where(a => a.Status == "Pending")
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex) when (ex.Message.Contains("Invalid object name 'LandlordApplications'"))
            {
                return new List<LandlordApplication>();
            }
        }

        private async Task<LandlordApplication?> GetLandlordApplicationSafeAsync(int id)
        {
            try
            {
                return await _context.LandlordApplications
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.Id == id);
            }
            catch (Exception ex) when (ex.Message.Contains("Invalid object name 'LandlordApplications'"))
            {
                return null;
            }
        }
    }
}
