using Microsoft.EntityFrameworkCore;
using DoAnLapTrinhWeb.Models;

namespace DoAnLapTrinhWeb.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Khai báo các bảng trong Database
        public DbSet<AppUser> Users { get; set; }
        public DbSet<Accommodation> Accommodations { get; set; }
        public DbSet<AccommodationImage> AccommodationImages { get; set; }
        public DbSet<AccommodationDocument> AccommodationDocuments { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<District> Districts { get; set; }

        // Dòng này được thêm vào để sửa lỗi đỏ CS1061
        public DbSet<Favorite> Favorites { get; set; }

        public DbSet<SearchLog> SearchLogs { get; set; }
        public DbSet<ApprovalHistory> ApprovalHistories { get; set; }
        public DbSet<AppNotification> Notifications { get; set; }
        public DbSet<RentalRequest> RentalRequests { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }

        // Bảng Chat
        public DbSet<ChatConversation> ChatConversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppUser>()
                .Property(user => user.UserName)
                .HasMaxLength(40);

            modelBuilder.Entity<AppUser>()
                .HasIndex(user => user.UserName)
                .IsUnique();

            // Khắc phục cảnh báo vàng: Định dạng kích thước cho cột Price (18 chữ số, 0 số thập phân)
            modelBuilder.Entity<Accommodation>()
                .Property(a => a.Price)
                .HasColumnType("decimal(18,0)");

            modelBuilder.Entity<ApprovalHistory>()
                .Property(history => history.Price)
                .HasColumnType("decimal(18,0)");

            // Thiết lập quan hệ 1-N giữa User và Accommodation
            modelBuilder.Entity<Accommodation>()
                .HasOne(a => a.Owner)
                .WithMany(u => u.Accommodations)
                .HasForeignKey(a => a.OwnerId);

            // Accommodation lưu cả CityId và DistrictId, trong khi District cũng thuộc City.
            // Tắt cascade để tránh nhiều đường xóa City -> Accommodation trên SQL Server.
            modelBuilder.Entity<Accommodation>()
                .HasOne(a => a.City)
                .WithMany()
                .HasForeignKey(a => a.CityId)
                .OnDelete(DeleteBehavior.NoAction);

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

            modelBuilder.Entity<AppNotification>()
                .HasOne(notification => notification.User)
                .WithMany()
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RentalRequest>()
                .HasOne(request => request.Renter)
                .WithMany()
                .HasForeignKey(request => request.RenterId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<RentalRequest>()
                .HasOne(request => request.Accommodation)
                .WithMany()
                .HasForeignKey(request => request.AccommodationId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================================================
            // CẤU HÌNH QUAN HỆ BẢNG CHAT
            // =========================================================
            modelBuilder.Entity<ChatConversation>()
                .HasOne(c => c.Renter)
                .WithMany()
                .HasForeignKey(c => c.RenterId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatConversation>()
                .HasOne(c => c.Landlord)
                .WithMany()
                .HasForeignKey(c => c.LandlordId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatConversation>()
                .HasOne(c => c.Accommodation)
                .WithMany()
                .HasForeignKey(c => c.AccommodationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
//hehe
