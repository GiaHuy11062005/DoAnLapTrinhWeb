using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize]
    public class LandlordApplicationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public LandlordApplicationController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await ResolveCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Info"] = "Tài khoản Admin không cần đăng ký làm chủ trọ.";
                return RedirectToAction("Index", "Admin");
            }

            if (string.Equals(user.Role, "Landlord", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Info"] = "Tài khoản của bạn đã là chủ trọ rồi.";
                return RedirectToAction("Update", "Profile");
            }

            var latestApplication = await GetLatestApplicationSafeAsync(user.Id);

            ViewBag.LatestApplication = latestApplication;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string phone,
            string note,
            IFormFile? citizenIdFrontImage,
            IFormFile? citizenIdBackImage,
            List<IFormFile>? supportingDocuments)
        {
            var user = await ResolveCurrentUserAsync();
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Info"] = "Tài khoản Admin không cần đăng ký làm chủ trọ.";
                return RedirectToAction("Index", "Admin");
            }

            if (string.Equals(user.Role, "Landlord", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Info"] = "Tài khoản của bạn đã là chủ trọ rồi.";
                return RedirectToAction("Update", "Profile");
            }

            if (string.IsNullOrWhiteSpace(phone) || citizenIdFrontImage == null || citizenIdBackImage == null)
            {
                TempData["Error"] = "Vui lòng nhập số điện thoại và tải lên đủ ảnh CCCD mặt trước/mặt sau.";
                return RedirectToAction(nameof(Create));
            }

            var existingPending = await HasPendingApplicationSafeAsync(user.Id);

            if (existingPending)
            {
                TempData["Info"] = "Bạn đang có một phiếu đăng ký chờ duyệt.";
                return RedirectToAction(nameof(Create));
            }

            var uploadRoot = Path.Combine(_webHostEnvironment.WebRootPath, "images", "landlord-applications");
            Directory.CreateDirectory(uploadRoot);

            string frontPath = await SaveImageAsync(citizenIdFrontImage, uploadRoot);
            string backPath = await SaveImageAsync(citizenIdBackImage, uploadRoot);
            var supportPaths = new List<string>();

            if (supportingDocuments != null)
            {
                foreach (var file in supportingDocuments.Where(f => f != null && f.Length > 0))
                {
                    supportPaths.Add(await SaveImageAsync(file, uploadRoot));
                }
            }

            var application = new LandlordApplication
            {
                UserId = user.Id,
                Phone = phone.Trim(),
                CitizenIdFrontImageUrl = frontPath,
                CitizenIdBackImageUrl = backPath,
                SupportingDocumentUrls = supportPaths.Count > 0 ? string.Join(';', supportPaths) : null,
                Note = note?.Trim(),
                Status = "Pending",
                CreatedAt = DateTime.Now
            };

            _context.LandlordApplications.Add(application);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gửi yêu cầu đăng ký làm chủ trọ. Vui lòng chờ admin duyệt.";
            return RedirectToAction(nameof(Create));
        }

        private async Task<string> SaveImageAsync(IFormFile file, string uploadRoot)
        {
            string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            string filePath = Path.Combine(uploadRoot, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/images/landlord-applications/{uniqueFileName}";
        }

        private async Task<AppUser?> ResolveCurrentUserAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out var userId))
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        private async Task<LandlordApplication?> GetLatestApplicationSafeAsync(int userId)
        {
            try
            {
                return await _context.LandlordApplications
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex) when (ex.Message.Contains("Invalid object name 'LandlordApplications'"))
            {
                return null;
            }
        }

        private async Task<bool> HasPendingApplicationSafeAsync(int userId)
        {
            try
            {
                return await _context.LandlordApplications
                    .AnyAsync(x => x.UserId == userId && x.Status == "Pending");
            }
            catch (Exception ex) when (ex.Message.Contains("Invalid object name 'LandlordApplications'"))
            {
                return false;
            }
        }
    }
}
