using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Đăng ký AppDbContext kết nối với SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =========================================================
// THÊM CẤU HÌNH COOKIE AUTHENTICATION CHO CHỨC NĂNG ĐĂNG NHẬP
// =========================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // Đường dẫn chuyển hướng khi chưa đăng nhập
        options.LogoutPath = "/Auth/Logout"; // Đường dẫn khi đăng xuất
        options.AccessDeniedPath = "/Auth/AccessDenied"; // Đường dẫn khi bị từ chối quyền truy cập (Role)
        options.Cookie.Name = "TienIchTroCookie"; // Tên cookie lưu trên trình duyệt của người dùng
        options.ExpireTimeSpan = TimeSpan.FromDays(30); // Giữ trạng thái đăng nhập tối đa 30 ngày
        options.SlidingExpiration = true; // Tự động gia hạn thời gian đăng nhập nếu người dùng đang dùng web
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrWhiteSpace(email))
                {
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var currentIdentity = context.Principal?.Identity as ClaimsIdentity;
                if (currentIdentity == null)
                {
                    return;
                }

                var currentName = context.Principal?.FindFirstValue(ClaimTypes.Name) ?? "";
                var currentRole = context.Principal?.FindFirstValue(ClaimTypes.Role) ?? "";
                var currentIdentityValue = context.Principal?.FindFirstValue("UserIdentity") ?? "";
                var shouldRefresh =
                    currentName != (user.FullName ?? "") ||
                    currentRole != (user.Role ?? "") ||
                    currentIdentityValue != (user.UserIdentity ?? "");

                if (!shouldRefresh)
                {
                    return;
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName ?? ""),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Role, user.Role ?? "User"),
                    new Claim("UserIdentity", user.UserIdentity ?? "")
                };

                var newPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
                context.ReplacePrincipal(newPrincipal);
                context.ShouldRenew = true;
            }
        };
    })
    .AddGoogle(googleOptions =>
     {
         // Các thông tin này bạn sẽ lấy trên Google Cloud Console khi tạo Credentials
         googleOptions.ClientId = "928387518191-5ek0gta5ltsrmvbk1f0j4aqeoccq7lgi.apps.googleusercontent.com";
         googleOptions.ClientSecret = "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";

         // Đường dẫn Google sẽ cấu hình trả kết quả về sau khi người dùng đăng nhập thành công
         googleOptions.CallbackPath = "/signin-google";
     });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// =========================================================
// THÊM MIDDLEWARE XÁC THỰC (Bắt buộc phải đứng trước UseAuthorization)
// =========================================================
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// =========================================================
// ĐOẠN CODE TỰ ĐỘNG ĐỒNG BỘ API & TẠO TÀI KHOẢN ADMIN
// =========================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();

        // Áp dụng các migration mới nhất trước khi seed dữ liệu
        await context.Database.MigrateAsync();

        // 1. Gọi hàm Seed Data kéo 3 Thành phố từ API
        await LocationSeeder.SeedLocationsAsync(context);

        // 2. Tự động tạo tài khoản Admin (Nếu chưa tồn tại)
        if (!context.Users.Any(u => u.Email == "admin@tienichtro.com"))
        {
            // Tạo mã băm mật khẩu SHA256 (Giống hệt hàm bên AuthController)
            string adminPasswordHash;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes("Admin@123"));
                var strBuilder = new System.Text.StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    strBuilder.Append(bytes[i].ToString("x2"));
                }
                adminPasswordHash = strBuilder.ToString();
            }

            // Thêm tài khoản Admin vào DB
            context.Users.Add(new DoAnLapTrinhWeb.Models.AppUser
            {
                FullName = "Quản Trị Viên",
                Email = "admin@tienichtro.com",
                PasswordHash = adminPasswordHash,
                Role = "Admin",
                IsEmailConfirmed = true // Admin thì không cần gửi mail xác thực
            });

            await context.SaveChangesAsync();
            Console.WriteLine("Đã tạo tài khoản Admin khởi tạo thành công!");
        }

        // 3. Seed một số phòng trọ mẫu để người dùng luôn thấy dữ liệu thật trên trang chủ/tìm kiếm
        await SampleAccommodationSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Có lỗi xảy ra khi tạo dữ liệu khởi tạo (Seeding).");
    }
}

app.Run();
