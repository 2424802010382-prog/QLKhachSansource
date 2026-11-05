namespace QLKhachSan.Migrations
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<QLKhachSan.Models.HotelDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            AutomaticMigrationDataLossAllowed = false;
        }

        protected override void Seed(QLKhachSan.Models.HotelDbContext context)
        {
            context.Rooms.AddOrUpdate(
    r => r.RoomName,

    new QLKhachSan.Models.Room
    {
        RoomName = "Phòng Deluxe",
        RoomType = "DELUXE",
        Price = 2000000,
        Capacity = 2,
        TotalRooms = 5,

        Description = "Phòng nghỉ sang trọng với nội thất hiện đại.",
        ImageUrl = "https://images.unsplash.com/photo-1566665797739-1674de7a421a?q=80&w=900",
        IsActive = true
    },

    new QLKhachSan.Models.Room
    {
        RoomName = "Phòng Premium",
        RoomType = "PREMIUM",
        Price = 2500000,
        Capacity = 2,
        TotalRooms = 5,

        Description = "Phòng cao cấp với không gian rộng rãi.",
        ImageUrl = "https://images.unsplash.com/photo-1611892440504-42a792e24d32?q=80&w=900",
        IsActive = true
    },

    new QLKhachSan.Models.Room
    {
        RoomName = "Luxury Balcony Suite",
        RoomType = "SUITE",
        Price = 3500000,
        Capacity = 2,
        TotalRooms = 5,

        Description = "Phòng suite sang trọng có ban công.",
        ImageUrl = "https://images.unsplash.com/photo-1590490360182-c33d57733427?q=80&w=900",
        IsActive = true
    },

    new QLKhachSan.Models.Room
    {
        RoomName = "Phòng Tổng Thống",
        RoomType = "PRESIDENT",
        Price = 8000000,
        Capacity = 4,
        TotalRooms = 5,

        Description = "Phòng cao cấp nhất của khách sạn.",
        ImageUrl = "https://images.unsplash.com/photo-1600607687920-4e2a09cf159d?q=80&w=900",
        IsActive = true
    },

    new QLKhachSan.Models.Room
    {
        RoomName = "Phòng Standard",
        RoomType = "STANDARD",
        Price = 800000,
        Capacity = 2,
        TotalRooms = 5,

        Description = "Phòng tiêu chuẩn đầy đủ tiện nghi.",
        ImageUrl = "https://images.unsplash.com/photo-1618773928121-c32242e63f39?q=80&w=900",
        IsActive = true
    },

    new QLKhachSan.Models.Room
    {
        RoomName = "Phòng Superior",
        RoomType = "SUPERIOR",
        Price = 1200000,
        Capacity = 2,
        TotalRooms = 5,

        Description = "Phòng rộng rãi, thoải mái.",
        ImageUrl = "https://images.unsplash.com/photo-1590490359683-658d3d23f972?q=80&w=900",
        IsActive = true
    },

    new QLKhachSan.Models.Room
    {
        RoomName = "Phòng Twin",
        RoomType = "TWIN ROOM",
        Price = 1000000,
        Capacity = 2,
        TotalRooms = 5,

        Description = "Phòng gồm hai giường đơn.",
        ImageUrl = "https://images.unsplash.com/photo-1595576508898-0ad5c879a061?q=80&w=900",
        IsActive = true
    },

    new QLKhachSan.Models.Room
    {
        RoomName = "Phòng Family",
        RoomType = "FAMILY ROOM",
        Price = 1500000,
        Capacity = 4,
        TotalRooms = 5,

        Description = "Phòng phù hợp cho gia đình.",
        ImageUrl = "https://images.unsplash.com/photo-1590490360182-c33d57733427?q=80&w=900",
        IsActive = true
    }


);
            string seedAdminPassword = Environment.GetEnvironmentVariable("LUXURY_HOTEL_SEED_ADMIN_PASSWORD");
            if (!context.Users.Any(u => u.Username == "admin") && !string.IsNullOrEmpty(seedAdminPassword) && seedAdminPassword.Length >= 12)
            {
                context.Users.Add(new QLKhachSan.Models.User
                {
                    Username = "admin",

                    PasswordHash =
                        System.Web.Helpers.Crypto.HashPassword(seedAdminPassword),

                    FullName = "Quản trị viên",

                    Email = "admin@luxuryhotel.com",

                    Role = "Admin",

                    IsActive = true,

                    CreatedAt = DateTime.Now
                });
            }

            if (!context.DichVus.Any())
            {
                context.DichVus.Add(
                    new QLKhachSan.Models.DichVu
                    {
                        TenDichVu = "Buffet sáng",
                        MoTa = "Buffet sáng tại nhà hàng của khách sạn.",
                        Gia = 150000,
                        DonVi = "người",
                        HoatDong = true
                    }
                );

                context.DichVus.Add(
                    new QLKhachSan.Models.DichVu
                    {
                        TenDichVu = "Spa thư giãn",
                        MoTa = "Gói spa thư giãn và chăm sóc cơ thể.",
                        Gia = 500000,
                        DonVi = "lần",
                        HoatDong = true
                    }
                );

                context.DichVus.Add(
                    new QLKhachSan.Models.DichVu
                    {
                        TenDichVu = "Đưa đón sân bay",
                        MoTa = "Dịch vụ đưa hoặc đón khách tại sân bay.",
                        Gia = 300000,
                        DonVi = "lượt",
                        HoatDong = true
                    }
                );

                context.DichVus.Add(
                    new QLKhachSan.Models.DichVu
                    {
                        TenDichVu = "Giặt ủi",
                        MoTa = "Dịch vụ giặt và ủi quần áo.",
                        Gia = 80000,
                        DonVi = "kg",
                        HoatDong = true
                    }
                );
            }


            // ==========================================================
            // DỮ LIỆU MẪU KHUYẾN MÃI
            // ==========================================================

            if (!context.KhuyenMais.Any())
            {
                context.KhuyenMais.Add(
                    new QLKhachSan.Models.KhuyenMai
                    {
                        MaCode = "WELCOME10",
                        TenKhuyenMai = "Ưu đãi chào mừng",
                        MoTa = "Giảm 10% tiền phòng, tối đa 500.000đ.",
                        LoaiGiamGia = "PhanTram",
                        GiaTriGiam = 10,
                        DonToiThieu = 1000000,
                        GiamToiDa = 500000,

                        NgayBatDau =
                            new DateTime(2026, 1, 1),

                        NgayKetThuc =
                            new DateTime(2026, 12, 31),

                        SoLuotSuDungToiDa = 100,
                        SoLuotDaSuDung = 0,
                        HoatDong = true
                    }
                );


                context.KhuyenMais.Add(
                    new QLKhachSan.Models.KhuyenMai
                    {
                        MaCode = "VIP500",
                        TenKhuyenMai = "Ưu đãi khách VIP",

                        MoTa =
                            "Giảm trực tiếp 500.000đ cho đơn từ 5.000.000đ.",

                        LoaiGiamGia = "SoTien",
                        GiaTriGiam = 500000,
                        DonToiThieu = 5000000,
                        GiamToiDa = null,

                        NgayBatDau =
                            new DateTime(2026, 1, 1),

                        NgayKetThuc =
                            new DateTime(2026, 12, 31),

                        SoLuotSuDungToiDa = 50,
                        SoLuotDaSuDung = 0,
                        HoatDong = true
                    }
                );
            }


            // Seed 40 physical rooms (5 rooms per room type)
            if (!context.PhysicalRooms.Any())
            {
                var dbRooms = context.Rooms.ToList();
                int floor = 1;
                foreach (var room in dbRooms)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        string roomNum = (floor * 100 + i).ToString();
                        context.PhysicalRooms.Add(new QLKhachSan.Models.PhysicalRoom
                        {
                            RoomTypeId = room.RoomId,
                            RoomNumber = roomNum,
                            Floor = floor,
                            Status = "Trống",
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        });
                    }
                    floor++;
                }
            }

            // Seed 12 default permissions
            if (!context.Permissions.Any())
            {
                var defaultPerms = new[]
                {
                    new QLKhachSan.Models.Permission { PermissionCode = "rooms.view", PermissionName = "Xem danh sách phòng", Category = "Phòng" },
                    new QLKhachSan.Models.Permission { PermissionCode = "rooms.manage", PermissionName = "Quản lý phòng (Thêm/Sửa/Trạng thái)", Category = "Phòng" },
                    new QLKhachSan.Models.Permission { PermissionCode = "bookings.view", PermissionName = "Xem danh sách đặt phòng", Category = "Đặt phòng" },
                    new QLKhachSan.Models.Permission { PermissionCode = "bookings.approve", PermissionName = "Xác nhận hoặc hủy đặt phòng", Category = "Đặt phòng" },
                    new QLKhachSan.Models.Permission { PermissionCode = "bookings.checkin", PermissionName = "Nhận phòng (Check-in)", Category = "Đặt phòng" },
                    new QLKhachSan.Models.Permission { PermissionCode = "bookings.checkout", PermissionName = "Trả phòng (Check-out)", Category = "Đặt phòng" },
                    new QLKhachSan.Models.Permission { PermissionCode = "customers.view", PermissionName = "Xem danh sách khách hàng", Category = "Khách hàng" },
                    new QLKhachSan.Models.Permission { PermissionCode = "services.manage", PermissionName = "Quản lý dịch vụ", Category = "Dịch vụ" },
                    new QLKhachSan.Models.Permission { PermissionCode = "restaurant.manage", PermissionName = "Quản lý nhà hàng & món ăn", Category = "Nhà hàng" },
                    new QLKhachSan.Models.Permission { PermissionCode = "restaurant.orders", PermissionName = "Xem và xử lý đơn gọi món F&B", Category = "Nhà hàng" },
                    new QLKhachSan.Models.Permission { PermissionCode = "payments.manage", PermissionName = "Quản lý hóa đơn & thanh toán", Category = "Tài chính" },
                    new QLKhachSan.Models.Permission { PermissionCode = "revenue.view", PermissionName = "Xem báo cáo doanh thu", Category = "Tài chính" }
                };
                context.Permissions.AddOrUpdate(p => p.PermissionCode, defaultPerms);
            }


            // ==========================================================
            // LƯU DỮ LIỆU
            // ==========================================================

            context.SaveChanges();
        }
    }
}
