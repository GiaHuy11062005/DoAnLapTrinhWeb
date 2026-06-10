using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DoAnLapTrinhWeb.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // PHẦN 1: GIAO DIỆN VÀ XỬ LÝ ĐĂNG KÝ
        // ==========================================
        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.Cities = _context.Cities.ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(AppUser user)
        {
            if (ModelState.IsValid)
            {
                var checkEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
                if (checkEmail != null)
                {
                    ViewBag.Error = "Email này đã được sử dụng!";
                    ViewBag.Cities = _context.Cities.ToList();
                    return View(user);
                }

                user.PasswordHash = HashPassword(user.PasswordHash ?? "");
                user.EmailConfirmationToken = Guid.NewGuid().ToString();

                // Set IsEmailConfirmed = false nhưng KHÔNG cấm đăng nhập
                user.IsEmailConfirmed = false;

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                string? confirmationLink = Url.Action("ConfirmEmail", "Auth", new { token = user.EmailConfirmationToken }, Request.Scheme);

                // Gửi email xác thực chạy ngầm
                await SendEmailAsync(user.Email!, "Xác nhận tài khoản Tiện Ích Trọ", $"Vui lòng click vào link sau để xác thực email của bạn: {confirmationLink}");

                // Đổi câu thông báo để hướng dẫn người dùng có thể đăng nhập ngay
                TempData["Success"] = "Đăng ký thành công! Bạn có thể đăng nhập ngay bây giờ. (Vui lòng kiểm tra hộp thư Email để xác thực tài khoản sau).";
                return RedirectToAction("Login");
            }

            ViewBag.Cities = _context.Cities.ToList();
            return View(user);
        }

        // ==========================================
        // PHẦN 2: GIAO DIỆN VÀ XỬ LÝ ĐĂNG NHẬP
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var hashedPassword = HashPassword(password ?? "");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hashedPassword);

            if (user != null)
            {
                // Ý TƯỞNG MỚI V2: Cố tình KHÔNG chặn if (!user.IsEmailConfirmed) ở đây
                // Người dùng có thể đăng nhập thoải mái dù chưa click link trong email.

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Name, user.FullName ?? "User"),
                    new Claim(ClaimTypes.Role, user.Role ?? "User"),
                    
                    // Lưu thêm định danh Sinh Viên / Người Đi Làm vào Claim để dùng cho Google Maps
                    new Claim("UserIdentity", user.UserIdentity ?? "")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Email hoặc mật khẩu không chính xác!";
            return View();
        }

        // ==========================================
        // PHẦN 3: ĐĂNG XUẤT VÀ XÁC NHẬN EMAIL
        // ==========================================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> ConfirmEmail(string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailConfirmationToken == token);
            if (user != null)
            {
                user.IsEmailConfirmed = true;
                user.EmailConfirmationToken = null;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Xác thực email thành công! Huy hiệu bảo mật của bạn đã được kích hoạt.";
                return RedirectToAction("Login");
            }
            return Content("Mã xác thực không hợp lệ hoặc đã hết hạn.");
        }

        // ==========================================
        // PHẦN 4: CÁC HÀM TIỆN ÍCH (HELPER METHODS)
        // ==========================================
        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                string senderEmail = "giahuy11062005@gmail.com";
                string appPassword = "cnxu kzxo rgqm hnre";

                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(senderEmail, appPassword),
                    EnableSsl = true,
                    UseDefaultCredentials = false
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "Hệ Thống Tiện Ích Trọ"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(toEmail);
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("LỖI GỬI EMAIL THẤT BẠI: " + ex.Message);
            }
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // ==========================================
        // PHẦN 5: ĐĂNG NHẬP GOOGLE VÀ HOÀN THIỆN HỒ SƠ
        // ==========================================
        [HttpGet]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleResponse")
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded) return RedirectToAction("Login");

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            var fullName = result.Principal.FindFirstValue(ClaimTypes.Name);

            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user != null)
            {
                // Tài khoản đã tồn tại → đăng nhập thẳng, không yêu cầu chọn vai trò
                // Nếu Role chưa có thì mặc định là "User"
                if (string.IsNullOrEmpty(user.Role))
                {
                    user.Role = "User";
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName ?? ""),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Role, user.Role ?? "User"),
                    new Claim("UserIdentity", user.UserIdentity ?? "")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                if (user.Role == "Admin") return RedirectToAction("Index", "Admin");
                return RedirectToAction("Index", "Home");
            }
            else
            {
                // Người dùng hoàn toàn mới → yêu cầu chọn định danh
                TempData["GoogleEmail"] = email;
                TempData["GoogleName"] = fullName;
                return RedirectToAction("CompleteProfile");
            }
        }

        [HttpGet]
        public IActionResult CompleteProfile()
        {
            var email = TempData["GoogleEmail"]?.ToString();
            var name = TempData["GoogleName"]?.ToString();

            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login");

            ViewBag.Email = email;
            ViewBag.Name = name;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CompleteProfile(string email, string fullName, string userIdentity)
        {
            // Trang CompleteProfile chỉ dành cho người dùng MỚI chưa có tài khoản
            // Role luôn là "User" - muốn trở thành Chủ trọ thì đăng ký trong trang Profile
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            AppUser userToLogin;

            if (existingUser != null)
            {
                // Trường hợp tài khoản đã tồn tại (không nên xảy ra nữa sau khi fix)
                // Chỉ cập nhật định danh nếu chưa có
                if (string.IsNullOrEmpty(existingUser.UserIdentity))
                    existingUser.UserIdentity = userIdentity;
                if (string.IsNullOrEmpty(existingUser.Role))
                    existingUser.Role = "User";

                _context.Users.Update(existingUser);
                userToLogin = existingUser;
            }
            else
            {
                // Tạo tài khoản mới với Role mặc định là "User"
                var newUser = new AppUser
                {
                    Email = email,
                    FullName = fullName,
                    Role = "User",
                    UserIdentity = userIdentity,
                    IsEmailConfirmed = true,
                    PasswordHash = "Google_No_Password"
                };

                _context.Users.Add(newUser);
                userToLogin = newUser;
            }

            await _context.SaveChangesAsync();

            // Cấp Cookie đăng nhập
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userToLogin.Id.ToString()),
                new Claim(ClaimTypes.Name, userToLogin.FullName ?? ""),
                new Claim(ClaimTypes.Email, userToLogin.Email ?? ""),
                new Claim(ClaimTypes.Role, userToLogin.Role ?? "User"),
                new Claim("UserIdentity", userToLogin.UserIdentity ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

    }
}
