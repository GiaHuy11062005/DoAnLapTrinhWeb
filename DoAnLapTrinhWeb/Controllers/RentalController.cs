using System.Security.Claims;
using DoAnLapTrinhWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize(Roles = "User")]
    public class RentalController : Controller
    {
        private readonly AppDbContext _context;

        public RentalController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> MyRentals()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var rentals = await _context.RentalRequests
                .AsNoTracking()
                .Include(request => request.Accommodation)
                    .ThenInclude(accommodation => accommodation!.Images)
                .Include(request => request.Accommodation)
                    .ThenInclude(accommodation => accommodation!.City)
                .Include(request => request.Accommodation)
                    .ThenInclude(accommodation => accommodation!.District)
                .Include(request => request.Accommodation)
                    .ThenInclude(accommodation => accommodation!.Owner)
                .Where(request =>
                    request.RenterId == userId &&
                    request.Status == "Success" &&
                    request.Accommodation != null)
                .OrderByDescending(request => request.ConfirmedAt ?? request.CreatedAt)
                .ToListAsync();

            var rentalKeys = rentals
                .Where(request => request.Accommodation != null)
                .Select(request => new { request.AccommodationId, request.RenterId, LandlordId = request.Accommodation!.OwnerId })
                .ToList();
            var conversations = await _context.ChatConversations
                .AsNoTracking()
                .Where(conversation => conversation.RenterId == userId)
                .Select(conversation => new { conversation.Id, conversation.AccommodationId, conversation.RenterId, conversation.LandlordId })
                .ToListAsync();
            ViewBag.ConversationLookup = conversations
                .Where(conversation => rentalKeys.Any(key =>
                    key.AccommodationId == conversation.AccommodationId &&
                    key.RenterId == conversation.RenterId &&
                    key.LandlordId == conversation.LandlordId))
                .GroupBy(conversation => $"{conversation.AccommodationId}:{conversation.RenterId}:{conversation.LandlordId}")
                .ToDictionary(group => group.Key, group => group.Max(conversation => conversation.Id));

            return View(rentals);
        }
    }
}
