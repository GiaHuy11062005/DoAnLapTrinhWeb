using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Models;

namespace DoAnLapTrinhWeb.Data
{
    public class AppDbContext : DbContext
    {
        // Hàm khởi tạo, cấu hình chuỗi kết nối (truyền từ Program.cs)
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // MỤC DATA: Khai báo các bảng trong Database (DbSet đại diện cho bảng)
        public DbSet<AppUser> Users { get; set; }
        public DbSet<Accommodation> Accommodations { get; set; }
        public DbSet<AccommodationImage> AccommodationImages { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<District> Districts { get; set; }
        public DbSet<Ward> Wards { get; set; }
        public DbSet<LandlordApplication> LandlordApplications { get; set; }

        // Dòng này được thêm vào để sửa lỗi đỏ CS1061
        public DbSet<Favorite> Favorites { get; set; }

        public DbSet<SearchLog> SearchLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<BookingRequest> BookingRequests { get; set; }

        // Hàm này dùng để cấu hình thủ công các quan hệ giữa các bảng (Ví dụ: 1-Nhiều)
        // và định nghĩa cách xóa dữ liệu (Cascade hoặc NoAction) để tránh lỗi vòng lặp
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Khắc phục cảnh báo vàng: Định dạng kích thước cho cột Price (18 chữ số, 0 số thập phân)
            modelBuilder.Entity<Accommodation>()
                .Property(a => a.Price)
                .HasColumnType("decimal(18,0)");

            // Thiết lập quan hệ 1-N giữa User và Accommodation
            modelBuilder.Entity<Accommodation>()
                .HasOne(a => a.Owner)
                .WithMany(u => u.Accommodations)
                .HasForeignKey(a => a.OwnerId);

            modelBuilder.Entity<Accommodation>()
                .HasOne(a => a.District)
                .WithMany()
                .HasForeignKey(a => a.DistrictId)
                .OnDelete(DeleteBehavior.NoAction);

            // KHẮC PHỤC LỖI CASCADE BẢNG FAVORITE (Yêu Thích)
            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.NoAction); // Tắt xóa dây chuyền từ User

            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.Accommodation)
                .WithMany()
                .HasForeignKey(f => f.AccommodationId)
                .OnDelete(DeleteBehavior.NoAction); // Tắt xóa dây chuyền từ Phòng

            modelBuilder.Entity<LandlordApplication>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<LandlordApplication>()
                .HasOne(a => a.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(a => a.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Ward>()
                .HasOne(w => w.District)
                .WithMany()
                .HasForeignKey(w => w.DistrictId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingRequest>()
                .HasOne(b => b.Student)
                .WithMany()
                .HasForeignKey(b => b.StudentId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<BookingRequest>()
                .HasOne(b => b.Landlord)
                .WithMany()
                .HasForeignKey(b => b.LandlordId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<BookingRequest>()
                .HasOne(b => b.Accommodation)
                .WithMany()
                .HasForeignKey(b => b.AccommodationId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
