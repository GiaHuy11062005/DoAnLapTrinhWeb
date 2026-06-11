using DoAnLapTrinhWeb.Data;
using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
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
        private const string GoogleExternalScheme = "GoogleExternal";
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
            user.Role = user.Role == "Landlord" ? "Landlord" : "User";
            user.UserName = user.UserName?.Trim().ToLowerInvariant();

            if (user.Role == "User" && string.IsNullOrWhiteSpace(user.UserIdentity))
            {
                ViewBag.Error = "Người tìm thuê cần chọn Sinh viên hoặc Người đi làm.";
                ViewBag.Cities = _context.Cities.ToList();
                return View(user);
            }

            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                ViewBag.Error = "Vui lòng nhập tên đăng nhập.";
                ViewBag.Cities = _context.Cities.ToList();
                return View(user);
            }

            if (ModelState.IsValid)
            {
                var existingAccount = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email == user.Email || u.UserName == user.UserName);
                if (existingAccount != null)
                {
                    ViewBag.Error = existingAccount.UserName == user.UserName
                        ? "Tên đăng nhập này đã được sử dụng!"
                        : "Email này đã được sử dụng!";
                    ViewBag.Cities = _context.Cities.ToList();
                    return View(user);
                }

                user.PasswordHash = HashPassword(user.PasswordHash ?? "");
                user.UserIdentity = user.Role == "User" ? user.UserIdentity : null;
                user.IsEmailConfirmed = false;
                user.EmailConfirmationToken = null;

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đăng ký thành công! Bạn có thể xác thực email sau trong trang cá nhân.";
                return RedirectToAction("Login");
            }

            ViewBag.Cities = _context.Cities.ToList();
            return View(user);
        }

        // ==========================================
        // PHẦN 2: GIAO DIỆN VÀ XỬ LÝ ĐĂNG NHẬP
        // ==========================================
        [HttpGet]
        public IActionResult Login(
            string? returnUrl = null,
            bool googleCancelled = false,
            bool googleLoginFailed = false)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            if (googleCancelled)
            {
                ViewBag.GoogleMessage = "Bạn đã hủy đăng nhập bằng Google.";
            }
            else if (googleLoginFailed)
            {
                ViewBag.Error = "Không thể đăng nhập bằng Google lúc này. Vui lòng thử lại.";
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string userName, string password, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            var hashedPassword = HashPassword(password ?? "");
            var normalizedLogin = userName.Trim().ToLowerInvariant();
            AppUser? user;

            try
            {
                user = await _context.Users.FirstOrDefaultAsync(u =>
                    (u.UserName == normalizedLogin || (u.Email != null && u.Email.ToLower() == normalizedLogin)) &&
                    u.PasswordHash == hashedPassword);
            }
            catch (Exception ex) when (ex.ToString().Contains("UserName", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "Database chưa có cột tên đăng nhập. Hãy chạy Update-Database rồi đăng nhập lại.";
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

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

                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                if (user.Role == "Admin")
                {
                    return RedirectToAction("Index", "Admin");
                }

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không chính xác!";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // ==========================================
        // PHẦN 3: ĐĂNG XUẤT VÀ XÁC NHẬN EMAIL
        // ==========================================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData.Clear();
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> ConfirmEmail(string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailConfirmationToken == token);
            if (user != null)
            {
                user.IsEmailConfirmed = true;
                user.EmailConfirmationToken = null;
                await _context.SaveChangesAsync();

                TempData["ProfileSuccess"] = "Xác thực email thành công! Tài khoản của bạn đã được xác minh.";
                return User.Identity?.IsAuthenticated == true
                    ? RedirectToAction("Index", "Profile")
                    : RedirectToAction("Login");
            }
            return Content("Mã xác thực không hợp lệ hoặc đã hết hạn.");
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SendVerificationEmail()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.IsEmailConfirmed)
            {
                TempData["ProfileSuccess"] = "Email của bạn đã được xác thực.";
                return RedirectToAction("Index", "Profile");
            }

            user.EmailConfirmationToken = Guid.NewGuid().ToString();
            await _context.SaveChangesAsync();

            var confirmationLink = Url.Action(
                "ConfirmEmail",
                "Auth",
                new { token = user.EmailConfirmationToken },
                Request.Scheme);

            var emailSent = await SendEmailAsync(
                user.Email!,
                "Xác nhận tài khoản Chỗ Ở Tiện Ích",
                $"Vui lòng nhấn vào liên kết sau để xác thực email: <a href=\"{confirmationLink}\">Xác thực email</a>");

            if (emailSent)
            {
                TempData["ProfileSuccess"] = "Đã gửi liên kết xác thực. Vui lòng kiểm tra hộp thư email.";
            }
            else
            {
                TempData["ProfileError"] = "Không thể gửi email xác thực lúc này. Vui lòng thử lại sau.";
            }
            return RedirectToAction("Index", "Profile");
        }

        // ==========================================
        // PHẦN 4: CÁC HÀM TIỆN ÍCH (HELPER METHODS)
        // ==========================================
        private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
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
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("LỖI GỬI EMAIL THẤT BẠI: " + ex.Message);
                return false;
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

        private string CreateGoogleUserName(string? email)
        {
            var prefix = email?.Split('@').FirstOrDefault() ?? "user";
            var safePrefix = new string(prefix
                .ToLowerInvariant()
                .Where(character => char.IsLetterOrDigit(character) || character == '_')
                .ToArray());

            if (string.IsNullOrWhiteSpace(safePrefix)) safePrefix = "user";
            return $"{safePrefix}_{Guid.NewGuid().ToString("N")[..6]}";
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
            var result = await HttpContext.AuthenticateAsync(GoogleExternalScheme);
            if (!result.Succeeded || result.Principal == null)
            {
                return RedirectToAction("Login", new { googleLoginFailed = true });
            }

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            var fullName = result.Principal.FindFirstValue(ClaimTypes.Name);
            await HttpContext.SignOutAsync(GoogleExternalScheme);

            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("Login", new { googleLoginFailed = true });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                // Google đã xác minh quyền sở hữu email, nên tài khoản cùng email được xem là đã xác thực.
                var accountChanged = false;
                if (!user.IsEmailConfirmed)
                {
                    user.IsEmailConfirmed = true;
                    user.EmailConfirmationToken = null;
                    accountChanged = true;
                }

                if (string.IsNullOrWhiteSpace(user.UserName))
                {
                    user.UserName = CreateGoogleUserName(email);
                    accountChanged = true;
                }

                if (accountChanged) await _context.SaveChangesAsync();

                // TÍNH NĂNG ÉP CHỌN ROLE:
                // Nếu tài khoản cũ chưa có Định danh V2 (Sinh viên/Đi làm) hoặc chưa có Role rõ ràng
                if (string.IsNullOrEmpty(user.Role) || (user.Role == "User" && string.IsNullOrEmpty(user.UserIdentity)))
                {
                    // Ép chuyển hướng sang trang Hoàn thiện hồ sơ
                    TempData["GoogleEmail"] = email;
                    TempData["GoogleName"] = user.FullName ?? fullName;
                    return RedirectToAction("CompleteProfile");
                }

                // Nếu đã đầy đủ thông tin thì cho đăng nhập bình thường
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName ?? ""),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Role, user.Role ?? ""),
                    new Claim("UserIdentity", user.UserIdentity ?? "")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                if (user.Role == "Admin") return RedirectToAction("Index", "Admin");
                return RedirectToAction("Index", "Home");
            }
            else
            {
                // Người mới tinh
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
        public async Task<IActionResult> CompleteProfile(string email, string fullName, string role, string userIdentity)
        {
            // Kiểm tra xem user này đã tồn tại trong DB chưa
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            AppUser userToLogin;

            if (existingUser != null)
            {
                // NẾU TÀI KHOẢN ĐÃ TỒN TẠI -> CẬP NHẬT DỮ LIỆU (KHÔNG CẦN SQL)
                existingUser.Role = role;
                existingUser.UserIdentity = userIdentity;
                existingUser.IsEmailConfirmed = true;
                existingUser.EmailConfirmationToken = null;

                _context.Users.Update(existingUser);
                userToLogin = existingUser;
            }
            else
            {
                // NẾU TÀI KHOẢN MỚI -> TẠO MỚI
                var newUser = new AppUser
                {
                    UserName = CreateGoogleUserName(email),
                    Email = email,
                    FullName = fullName,
                    Role = role,
                    UserIdentity = userIdentity, // Lưu định danh Sinh viên/Người đi làm
                    IsEmailConfirmed = true,
                    PasswordHash = "Google_No_Password"
                };

                _context.Users.Add(newUser);
                userToLogin = newUser;
            }

            await _context.SaveChangesAsync();

            // Cấp Cookie đăng nhập với quyền vừa chọn
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userToLogin.Id.ToString()),
                new Claim(ClaimTypes.Name, userToLogin.FullName ?? ""),
                new Claim(ClaimTypes.Email, userToLogin.Email ?? ""),
                new Claim(ClaimTypes.Role, userToLogin.Role ?? ""),
                new Claim("UserIdentity", userToLogin.UserIdentity ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            if (userToLogin.Role == "Admin") return RedirectToAction("Index", "Admin");
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // CÔNG CỤ RESET QUYỀN (Không cần mở SQL)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ResetMyRole()
        {
            // Lấy email của tài khoản đang đăng nhập hiện tại
            var email = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
                return Content("Bạn chưa đăng nhập! Hãy đăng nhập trước.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                // Xóa sạch Role và Định danh cũ trong Database
                user.Role = null;
                user.UserIdentity = null;
                await _context.SaveChangesAsync();

                // Đăng xuất để xóa luôn Cookie cũ
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                return Content("Đã xóa quyền cũ thành công! Bây giờ bạn hãy quay lại Trang chủ, bấm Đăng nhập bằng Google, Form chọn Chủ trọ sẽ hiện ra.");
            }

            return Content("Không tìm thấy tài khoản trong hệ thống.");
        }
    }
}
