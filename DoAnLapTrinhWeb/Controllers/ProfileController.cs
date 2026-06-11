using DoAnLapTrinhWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return NotFound();

            ViewBag.Cities = await _context.Cities.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Districts = user.CityId.HasValue
                ? await _context.Districts.Where(d => d.CityId == user.CityId).OrderBy(d => d.Name).ToListAsync()
                : [];

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Update(
            string fullName,
            string? phone,
            int? cityId,
            int? districtId,
            string? userIdentity,
            string? destinationAddress,
            IFormFile? avatarFile)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return NotFound();

            user.FullName = fullName?.Trim();
            user.Phone = phone?.Trim();
            user.CityId = cityId;
            user.DistrictId = districtId;
            user.DestinationAddress = destinationAddress?.Trim();
            user.UserIdentity = user.Role == "User" ? userIdentity : null;

            if (avatarFile != null && avatarFile.Length > 0)
            {
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!allowedTypes.Contains(avatarFile.ContentType) || avatarFile.Length > 5 * 1024 * 1024)
                {
                    TempData["ProfileError"] = "Avatar chỉ nhận JPG, PNG hoặc WEBP và tối đa 5MB.";
                    return RedirectToAction(nameof(Index));
                }

                var extension = avatarFile.ContentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => throw new InvalidOperationException("Loại avatar không hợp lệ.")
                };
                var avatarFolder = Path.Combine(_environment.WebRootPath, "images", "avatars");
                Directory.CreateDirectory(avatarFolder);
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(avatarFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(stream);
                }

                var oldAvatarPath = string.IsNullOrWhiteSpace(user.AvatarUrl)
                    ? null
                    : Path.Combine(avatarFolder, Path.GetFileName(user.AvatarUrl));
                user.AvatarUrl = $"/images/avatars/{fileName}";
                await _context.SaveChangesAsync();

                if (oldAvatarPath != null && System.IO.File.Exists(oldAvatarPath))
                {
                    System.IO.File.Delete(oldAvatarPath);
                }

                TempData["ProfileSuccess"] = "Đã cập nhật thông tin cá nhân và avatar.";
                return RedirectToAction(nameof(Index));
            }

            await _context.SaveChangesAsync();
            TempData["ProfileSuccess"] = "Đã cập nhật thông tin cá nhân.";
            return RedirectToAction(nameof(Index));
        }

        private int? GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId)
                ? userId
                : null;
        }
    }
}
