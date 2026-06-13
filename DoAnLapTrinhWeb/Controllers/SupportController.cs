using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using DoAnLapTrinhWeb.Hubs;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Linq;

namespace DoAnLapTrinhWeb.Controllers
{
    public class SupportController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IHubContext<AccommodationHub> _hubContext;

        public SupportController(AppDbContext context, IWebHostEnvironment environment, IHubContext<AccommodationHub> hubContext)
        {
            _context = context;
            _environment = environment;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new SupportTicket();

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdString, out int userId))
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user != null)
                    {
                        model.FullName = user.FullName ?? string.Empty;
                        model.Email = user.Email ?? string.Empty;
                    }
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupportTicket model, System.Collections.Generic.List<IFormFile>? attachments)
        {
            ModelState.Remove("User");
            ModelState.Remove("Status");
            ModelState.Remove("AttachedImagePath");
            
            if (ModelState.IsValid)
            {
                if (attachments != null && attachments.Count > 5)
                {
                    ModelState.AddModelError("", "Chỉ được phép tải lên tối đa 5 ảnh đính kèm.");
                    return View(model);
                }

                if (User.Identity != null && User.Identity.IsAuthenticated)
                {
                    var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(userIdString, out int userId))
                    {
                        model.UserId = userId;
                    }
                }

                // Handle Image Upload
                if (attachments != null && attachments.Count > 0)
                {
                    var uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "support");
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    var imagePaths = new System.Collections.Generic.List<string>();

                    foreach (var attachment in attachments)
                    {
                        if (attachment.Length > 0)
                        {
                            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(attachment.FileName);
                            var filePath = Path.Combine(uploadFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await attachment.CopyToAsync(fileStream);
                            }

                            imagePaths.Add("/uploads/support/" + uniqueFileName);
                        }
                    }

                    if (imagePaths.Any())
                    {
                        model.AttachedImagePath = string.Join("|", imagePaths);
                    }
                }

                _context.SupportTickets.Add(model);
                await _context.SaveChangesAsync();

                // Notify all Admins
                var adminUsers = await _context.Users.Where(u => u.Role == "Admin").ToListAsync();
                foreach (var admin in adminUsers)
                {
                    var notification = new AppNotification
                    {
                        UserId = admin.Id,
                        Type = "SupportTicket",
                        Title = "Yêu cầu hỗ trợ mới",
                        Message = $"Từ {model.FullName}: {model.Subject}",
                        Url = "/Admin/SupportTickets"
                    };
                    _context.Notifications.Add(notification);
                }
                await _context.SaveChangesAsync();

                // Send real-time notification to the Admins group
                await _hubContext.Clients.Group("Admins").SendAsync("NotificationReceived", new
                {
                    Type = "SupportTicket",
                    Title = "Yêu cầu hỗ trợ mới",
                    Message = $"Từ {model.FullName}",
                    Url = "/Admin/SupportTickets",
                    IsRead = false,
                    createdAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                });

                TempData["SuccessMessage"] = "Yêu cầu hỗ trợ của bạn đã được gửi thành công. Admin sẽ phản hồi trong thời gian sớm nhất.";
                return RedirectToAction(nameof(Create));
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MyTickets()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var tickets = await _context.SupportTickets
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        [HttpGet]
        public async Task<IActionResult> TicketDetails(int id)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var ticket = await _context.SupportTickets
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }
    }
}
