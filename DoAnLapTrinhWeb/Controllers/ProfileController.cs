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

        public ProfileController(AppDbContext context)
        {
            _context = context;
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
        public async Task<IActionResult> Update(string fullName, string? phone, int? cityId, int? districtId, string? userIdentity, string? destinationAddress)
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
