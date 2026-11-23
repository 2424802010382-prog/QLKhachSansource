using System.Data.Entity;

namespace QLKhachSan.Models
{
    public class HotelDbContext : DbContext
    {
        public HotelDbContext()
            : base("HotelDbContext")
        {
        }

        public HotelDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
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
        public DbSet<RoomServiceCatalog> RoomServiceCatalogs { get; set; }
        public DbSet<RoomServiceProduct> RoomServiceProducts { get; set; }
        public DbSet<RoomServiceOrder> RoomServiceOrders { get; set; }
        public DbSet<RoomServiceHistory> RoomServiceHistories { get; set; }
        public DbSet<RoomServiceBedPolicy> RoomServiceBedPolicies { get; set; }
        public DbSet<SePayTransaction> SePayTransactions { get; set; }
        public DbSet<SePayEnvironment> SePayEnvironments { get; set; }
        public DbSet<ChiTietDonDatMon> ChiTietDonDatMons { get; set; }
        public DbSet<DanhMucMonAn> DanhMucMonAns { get; set; }
        public DbSet<MonAn> MonAns { get; set; }
        public DbSet<DonDatMon> DonDatMons { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<HotelAutoCancelAudit> HotelAutoCancelAudits { get; set; }
        public DbSet<RefundRequest> RefundRequests { get; set; }
        public DbSet<RefundTransaction> RefundTransactions { get; set; }

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
            modelBuilder.Entity<RoomServiceCatalog>().HasKey(e => e.MaDichVu).ToTable("RoomServiceCatalog");
            modelBuilder.Entity<RoomServiceCatalog>()
                .HasRequired(r => r.DichVu)
                .WithOptional(d => d.RoomServiceCatalog);
            modelBuilder.Entity<RoomServiceProduct>().HasKey(e => e.ProductId).ToTable("RoomServiceProducts");
            modelBuilder.Entity<RoomServiceOrder>().HasKey(e => e.OrderId).ToTable("RoomServiceOrders");
            modelBuilder.Entity<RoomServiceHistory>().HasKey(e => e.EventId).ToTable("RoomServiceHistory");
            modelBuilder.Entity<RoomServiceBedPolicy>().HasKey(e => e.RoomId).ToTable("RoomServiceBedPolicy");
            modelBuilder.Entity<RoomServiceBedPolicy>()
                .HasRequired(r => r.Room)
                .WithOptional(rm => rm.RoomServiceBedPolicy);
            modelBuilder.Entity<SePayTransaction>().HasKey(e => e.Id).ToTable("SePayTransactions");
            modelBuilder.Entity<SePayEnvironment>().HasKey(e => e.Id).ToTable("SePayEnvironment");
            modelBuilder.Entity<ChiTietDonDatMon>().HasKey(e => e.MaChiTiet).ToTable("ChiTietDonDatMon");
            modelBuilder.Entity<DanhMucMonAn>().HasKey(e => e.MaDanhMuc).ToTable("DanhMucMonAn");
            modelBuilder.Entity<MonAn>().HasKey(e => e.MaMonAn).ToTable("MonAn");
            modelBuilder.Entity<DonDatMon>().HasKey(e => e.MaDon).ToTable("DonDatMon");
            modelBuilder.Entity<PasswordResetToken>().HasKey(e => e.ResetTokenId).ToTable("PasswordResetTokens");
            modelBuilder.Entity<HotelAutoCancelAudit>().HasKey(e => e.AuditId).ToTable("HotelAutoCancelAudits");
            modelBuilder.Entity<RefundRequest>().HasKey(e => e.RefundRequestId).ToTable("RefundRequests");
            modelBuilder.Entity<RefundTransaction>().HasKey(e => e.RefundTransactionId).ToTable("RefundTransactions");
        }
    }
}
