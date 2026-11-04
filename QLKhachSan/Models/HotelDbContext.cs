using System.Data.Entity;

namespace QLKhachSan.Models
{
    public class HotelDbContext : DbContext
    {
        public HotelDbContext()
            : base("HotelDbContext")
        {
        }

        public DbSet<Room> Rooms { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<DichVu> DichVus { get; set; }
        public DbSet<KhuyenMai> KhuyenMais { get; set; }
        public DbSet<DatPhongDichVu> DatPhongDichVus { get; set; }
        public DbSet<ThanhToan> ThanhToans { get; set; }
        public DbSet<DanhGia> DanhGias { get; set; }
        public DbSet<PhysicalRoom> PhysicalRooms { get; set; }
        public DbSet<RoomMaintenance> RoomMaintenances { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Room>().HasKey(e => e.RoomId).ToTable("Rooms");
            modelBuilder.Entity<Booking>().HasKey(e => e.BookingId).ToTable("Bookings");
            modelBuilder.Entity<User>().HasKey(e => e.UserId).ToTable("Users");
            modelBuilder.Entity<DanhGia>().HasKey(e => e.MaDanhGia).ToTable("DanhGia");
            modelBuilder.Entity<DatPhongDichVu>().HasKey(e => e.MaDatPhongDichVu).ToTable("DatPhongDichVu");
            modelBuilder.Entity<DichVu>().HasKey(e => e.MaDichVu).ToTable("DichVu");
            modelBuilder.Entity<KhuyenMai>().HasKey(e => e.MaKhuyenMai).ToTable("KhuyenMai");
            modelBuilder.Entity<ThanhToan>().HasKey(e => e.MaThanhToan).ToTable("ThanhToan");
            modelBuilder.Entity<PhysicalRoom>().HasKey(e => e.PhysicalRoomId).ToTable("PhysicalRooms");
            modelBuilder.Entity<RoomMaintenance>().HasKey(e => e.MaintenanceId).ToTable("RoomMaintenances");
            modelBuilder.Entity<Permission>().HasKey(e => e.PermissionId).ToTable("Permissions");
            modelBuilder.Entity<UserPermission>().HasKey(e => e.UserPermissionId).ToTable("UserPermissions");
        }
    }
}
