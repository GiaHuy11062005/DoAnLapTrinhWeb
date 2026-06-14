using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DoAnLapTrinhWeb.Data;

namespace DoAnLapTrinhWeb.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly AppDbContext _context;

        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            if (int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"ChatUser_{userId}");
            }
            await base.OnConnectedAsync();
        }

        public async Task JoinConversation(int conversationId)
        {
            if (!int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;

            var canJoin = await _context.ChatConversations.AnyAsync(item =>
                item.Id == conversationId &&
                (item.RenterId == userId || item.LandlordId == userId));
            if (!canJoin) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Chat_{conversationId}");
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Chat_{conversationId}");
        }
    }
}
