using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Update()
        {
            var userId = await ResolveCurrentUserIdAsync();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            ViewBag.Cities = await _context.Cities.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CurrentDistrictName = user.DistrictId.HasValue
                ? await _context.Districts.Where(d => d.Id == user.DistrictId.Value).Select(d => d.Name).FirstOrDefaultAsync()
                : null;
            ViewBag.LatestLandlordApplication = await GetLatestLandlordApplicationSafeAsync(user.Id);

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> Update(int id, string? fullName, string? phone, int? cityId, int? districtId, string? destinationAddress, string? userIdentity)
        {
            var userId = await ResolveCurrentUserIdAsync();
            if (!userId.HasValue || userId.Value != id)
            {
                return Unauthorized();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (user == null)
            {
                return NotFound();
            }

            user.FullName = fullName?.Trim();
            user.Phone = phone?.Trim();
            user.CityId = cityId;
            user.DistrictId = districtId;
            user.DestinationAddress = destinationAddress?.Trim();
            user.UserIdentity = userIdentity;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Role, user.Role ?? "User"),
                new Claim("UserIdentity", user.UserIdentity ?? "")
            };

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

            return RedirectToAction("Update");
        }

        private async Task<int?> ResolveCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            return user?.Id;
        }

        private async Task<LandlordApplication?> GetLatestLandlordApplicationSafeAsync(int userId)
        {
            try
            {
                return await _context.LandlordApplications
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex) when (ex.Message.Contains("Invalid object name 'LandlordApplications'"))
            {
                return null;
            }
        }
    }
}
