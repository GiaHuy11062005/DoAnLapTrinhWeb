using Microsoft.AspNetCore.Mvc;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using System;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize]
    public class ContactController : Controller
    {
        private readonly AppDbContext _context;

        public ContactController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(int receiverId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Nội dung tin nhắn không được để trống." });
            }

            var senderIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(senderIdString, out int senderId))
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để gửi tin nhắn." });
            }

            if (senderId == receiverId)
            {
                return Json(new { success = false, message = "Bạn không thể tự gửi tin nhắn cho chính mình." });
            }

            var contact = new Contact
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã gửi tin nhắn thành công!" });
        }
    }
}
