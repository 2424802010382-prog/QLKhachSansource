using QLKhachSan.Models;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;
using System;
using System.IO;
using System.Web;
namespace QLKhachSan.Controllers
{
    public class RestaurantController : HotelController
    {
private class FoodPriceRow
        {
            public int MaMonAn { get; set; }

            public string TenMonAn { get; set; }

            public decimal Gia { get; set; }

            public bool HoatDong { get; set; }
        }
        // =========================================================
        // TRANG NHÀ HÀNG
        // =========================================================

        [HttpGet]
        public ActionResult Index()
        {
            // =====================================================
            // 1. LẤY DANH MỤC
            // =====================================================

            string categorySql =
                @"
                    SELECT
                        MaDanhMuc,
                        TenDanhMuc,
                        MoTa,
                        ThuTu

                    FROM dbo.DanhMucMonAn

                    WHERE HoatDong = 1

                    ORDER BY
                        ThuTu,
                        TenDanhMuc
                ";


            List<RestaurantCategoryViewModel>
                categories =

                db.Database
                  .SqlQuery<RestaurantCategoryViewModel>(
                      categorySql
                  )
                  .ToList();


            // =====================================================
            // 2. LẤY MÓN ĂN
            // =====================================================

            string foodSql =
                @"
                    SELECT
                        m.MaMonAn,
                        m.MaDanhMuc,
                        d.TenDanhMuc,
                        m.TenMonAn,
                        m.MoTa,
                        m.Gia,
                        m.HinhAnh,
                        m.DonVi,
                        m.ThoiGianChuanBi,
                        m.NoiBat

                    FROM dbo.MonAn AS m

                    INNER JOIN dbo.DanhMucMonAn AS d
                        ON m.MaDanhMuc =
                           d.MaDanhMuc

                    WHERE
                        m.HoatDong = 1
                        AND d.HoatDong = 1

                    ORDER BY
                        m.NoiBat DESC,
                        d.ThuTu,
                        m.TenMonAn
                ";


            List<RestaurantFoodViewModel>
                foods =

                db.Database
                  .SqlQuery<RestaurantFoodViewModel>(
                      foodSql
                  )
                  .ToList();

            foreach (var f in foods)
            {
                f.HinhAnh = GetSmartFoodImage(f.TenMonAn, f.HinhAnh);
            }


            // =====================================================
            // 3. TẠO MODEL
            // =====================================================

            RestaurantIndexViewModel model =
                new RestaurantIndexViewModel
                {
                    Categories =
                        categories,

                    Foods =
                        foods
                };


            return View(model);
        }


        // =========================================================
        // GIẢI PHÓNG DATABASE
        // =========================================================
        // =========================================================
        // ĐẶT MÓN
        // =========================================================

        [HttpPost]
        public JsonResult PlaceOrder(
            RestaurantOrderRequest request
        )
        {
            string role = Session["Role"] != null ? Session["Role"].ToString() : "";
            if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase) || role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Tài khoản quản trị hoặc nhân viên không được phép đặt món với tư cách khách hàng." });
            }

            // =====================================================
            // 1. KIỂM TRA REQUEST
            // =====================================================

            if (request == null)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Dữ liệu đặt món không hợp lệ."
                });
            }


            // =====================================================
            // 2. CHUẨN HÓA
            // =====================================================

            string hoTen =
                (request.HoTen ?? "")
                .Trim();


            string soDienThoai =
                (request.SoDienThoai ?? "")
                .Trim();


            string hinhThuc =
                (request.HinhThuc ?? "")
                .Trim();


            string soBan =
                string.IsNullOrWhiteSpace(
                    request.SoBan
                )
                ? null
                : request.SoBan.Trim();


            string soPhong =
                string.IsNullOrWhiteSpace(
                    request.SoPhong
                )
                ? null
                : request.SoPhong.Trim();


            string ghiChu =
                string.IsNullOrWhiteSpace(
                    request.GhiChu
                )
                ? null
                : request.GhiChu.Trim();


            string paymentMethod =
                (request.PhuongThucThanhToan ?? "")
                .Trim();


            // =====================================================
            // 3. KIỂM TRA THÔNG TIN KHÁCH
            // =====================================================

            if (
                string.IsNullOrWhiteSpace(
                    hoTen
                )
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Vui lòng nhập họ tên."
                });
            }


            if (
                string.IsNullOrWhiteSpace(
                    soDienThoai
                )
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Vui lòng nhập số điện thoại."
                });
            }


            if (
                soDienThoai.Length < 10 ||
                soDienThoai.Length > 11 ||
                !soDienThoai.All(
                    char.IsDigit
                )
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Số điện thoại phải gồm 10 đến 11 chữ số."
                });
            }


            // =====================================================
            // 4. KIỂM TRA HÌNH THỨC NHẬN MÓN
            // =====================================================

            if (
                hinhThuc !=
                "Ăn tại nhà hàng"
                &&
                hinhThuc !=
                "Giao lên phòng"
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Hình thức nhận món không hợp lệ."
                });
            }


            if (
                hinhThuc ==
                "Ăn tại nhà hàng"
                &&
                string.IsNullOrWhiteSpace(
                    soBan
                )
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Vui lòng nhập số bàn."
                });
            }


            if (
                hinhThuc ==
                "Giao lên phòng"
                &&
                string.IsNullOrWhiteSpace(
                    soPhong
                )
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Vui lòng chọn phòng lưu trú."
                });
            }

            int? roomBookingId = null;
            if (hinhThuc == "Giao lên phòng")
            {
                if (string.IsNullOrWhiteSpace(soPhong))
                {
                    return Json(new { success = false, message = "Vui lòng chọn phòng lưu trú." });
                }

                int roomUserId;
                if (!int.TryParse(Convert.ToString(Session["UserId"]), out roomUserId))
                    return Json(new { success = false, message = "Vui lòng đăng nhập để gọi món lên phòng." });
                var ownedIds = db.Database.SqlQuery<int>("SELECT BookingId FROM dbo.Bookings WHERE UserId=@p0", roomUserId).ToList();
                DateTime today = DateTime.Today;
                var validBooking = db.Bookings.AsNoTracking().Where(b => ownedIds.Contains(b.BookingId) && b.Status == "Đang ở" &&
                        b.CheckInDate <= today && b.CheckOutDate > today).ToList()
                    .FirstOrDefault(b => (!request.BookingId.HasValue || request.BookingId == b.BookingId) &&
                        PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, soPhong));
                if (validBooking != null) roomBookingId = validBooking.BookingId;

                if (validBooking == null)
                {
                    return Json(new { success = false, message = $"Phòng [{soPhong}] không thuộc đơn đặt phòng hợp lệ của bạn hoặc bạn chưa nhận phòng!" });
                }
            }


            // =====================================================
            // 5. THANH TOÁN
            // =====================================================

            if (request.ChargeToRoom && !roomBookingId.HasValue)
                return Json(new { success = false, message = "Chỉ khách đang ở mới có thể cộng món vào hóa đơn phòng." });
            if (request.ChargeToRoom) paymentMethod = "Thanh toán cùng phòng";
            if (
                paymentMethod !=
                "Thanh toán khi nhận món"
                &&
                paymentMethod !=
                "Chuyển khoản" && !(request.ChargeToRoom && paymentMethod == "Thanh toán cùng phòng")
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Phương thức thanh toán không hợp lệ."
                });
            }


            // =====================================================
            // 6. KIỂM TRA GIỎ MÓN
            // =====================================================

            if (
                request.Items == null ||
                request.Items.Count == 0
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Giỏ món đang trống."
                });
            }


            // Không cho số lượng <= 0
            if (
                request.Items.Any(
                    x =>
                    x == null || x.SoLuong <= 0
                    ||
                    x.SoLuong > 50
                )
            )
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Số lượng món không hợp lệ."
                });
            }


            // =====================================================
            // 7. GỘP MÓN TRÙNG ID
            // =====================================================

            var itemRequests =
                request.Items
                    .GroupBy(
                        x => x.MaMonAn
                    )
                    .Select(
                        g =>
                        new RestaurantOrderItemRequest
                        {
                            MaMonAn =
                                g.Key,

                            SoLuong =
                                g.Sum(
                                    x => x.SoLuong
                                )
                        }
                    )
                    .ToList();


            // =====================================================
            // 8. LẤY ID MÓN
            // =====================================================

            if (itemRequests.Any(x => x.SoLuong > 50))
                return Json(new { success = false, message = "Tổng số lượng mỗi món không được vượt quá 50." });
            List<int> foodIds =
                itemRequests
                    .Select(
                        x => x.MaMonAn
                    )
                    .Distinct()
                    .ToList();


            // =====================================================
            // 9. ĐỌC GIÁ THẬT TỪ DATABASE
            // =====================================================

            List<FoodPriceRow> foods =
                new List<FoodPriceRow>();


            foreach (
                int foodId
                in foodIds
            )
            {
                FoodPriceRow food =
                    db.Database
                      .SqlQuery<FoodPriceRow>(
                          @"
                    SELECT TOP 1
                        MaMonAn,
                        TenMonAn,
                        Gia,
                        HoatDong

                    FROM dbo.MonAn

                    WHERE MaMonAn = @p0
                  ",
                          foodId
                      )
                      .FirstOrDefault();


                if (
                    food == null ||
                    !food.HoatDong
                )
                {
                    return Json(new
                    {
                        success = false,

                        message =
                            "Có món ăn không tồn tại hoặc đã ngừng phục vụ."
                    });
                }


                foods.Add(
                    food
                );
            }


            // =====================================================
            // 10. TÍNH TỔNG TIỀN TỪ SERVER
            // =====================================================

            decimal totalAmount =
                0;


            foreach (
                var item
                in itemRequests
            )
            {
                FoodPriceRow food =
                    foods.First(
                        x =>
                        x.MaMonAn ==
                        item.MaMonAn
                    );


                totalAmount +=
                    food.Gia *
                    item.SoLuong;
            }


            if (totalAmount <= 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Tổng tiền đơn món không hợp lệ."
                });
            }


            // =====================================================
            // 11. USER ID
            // =====================================================

            int? userId =
                null;


            if (
                Session["UserId"] != null
            )
            {
                int parsedUserId;


                if (
                    int.TryParse(
                        Session["UserId"]
                            .ToString(),

                        out parsedUserId
                    )
                )
                {
                    userId =
                        parsedUserId;
                }
            }


            object userIdValue =
                userId.HasValue

                ? (object)userId.Value

                : DBNull.Value;


            // BookingId tạm thời chưa liên kết.
            object bookingIdValue = roomBookingId.HasValue ? (object)roomBookingId.Value : DBNull.Value;


            // =====================================================
            // 12. TRẠNG THÁI THANH TOÁN
            // =====================================================

            string paymentStatus =
                "Chưa thanh toán";


            // =====================================================
            // 13. TRANSACTION
            // =====================================================

            using (
                var transaction =
                    db.Database.BeginTransaction()
            )
            {
                try
                {
                    // =================================================
                    // TẠO ĐƠN
                    // =================================================

                    HotelOperations.LockInventory(db);
                    Booking roomBooking = null;
                    if (roomBookingId.HasValue)
                    {
                        roomBooking = db.Bookings.Find(roomBookingId.Value);
                        if (roomBooking == null || roomBooking.Status != "Đang ở" || roomBooking.CheckOutDate <= DateTime.Today)
                            return Json(new { success = false, message = "Khách đã trả phòng hoặc đơn không còn lưu trú." });
                    }
                    int orderId =
                        db.Database
                          .SqlQuery<int>(
                              @"
                        INSERT INTO dbo.DonDatMon
                        (
                            UserId,
                            BookingId,
                            HoTen,
                            SoDienThoai,
                            HinhThuc,
                            SoBan,
                            SoPhong,
                            GhiChu,
                            TongTien,
                            TrangThai,
                            PhuongThucThanhToan,
                            TrangThaiThanhToan,
                            CreatedAt
                        )

                        VALUES
                        (
                            @p0,
                            @p1,
                            @p2,
                            @p3,
                            @p4,
                            @p5,
                            @p6,
                            @p7,
                            @p8,
                            N'Chờ xác nhận',
                            @p9,
                            @p10,
                            GETDATE()
                        );

                        SELECT
                            CAST(
                                SCOPE_IDENTITY()
                                AS INT
                            );
                      ",

                              userIdValue,
                              bookingIdValue,
                              hoTen,
                              soDienThoai,
                              hinhThuc,
                              (object)soBan ??
                                  DBNull.Value,
                              (object)soPhong ??
                                  DBNull.Value,
                              (object)ghiChu ??
                                  DBNull.Value,
                              totalAmount,
                              paymentMethod,
                              paymentStatus
                          )
                          .First();


                    // =================================================
                    // CHI TIẾT MÓN
                    // =================================================

                    foreach (
                        var item
                        in itemRequests
                    )
                    {
                        FoodPriceRow food =
                            foods.First(
                                x =>
                                x.MaMonAn ==
                                item.MaMonAn
                            );


                        decimal lineTotal =
                            food.Gia *
                            item.SoLuong;


                        db.Database.ExecuteSqlCommand(
                            @"
                        INSERT INTO dbo.ChiTietDonDatMon
                        (
                            MaDon,
                            MaMonAn,
                            SoLuong,
                            DonGia,
                            ThanhTien
                        )

                        VALUES
                        (
                            @p0,
                            @p1,
                            @p2,
                            @p3,
                            @p4
                        )
                    ",

                            orderId,
                            item.MaMonAn,
                            item.SoLuong,
                            food.Gia,
                            lineTotal
                        );
                    }


                    if (request.ChargeToRoom)
                    {
                        var roomService = db.DichVus.FirstOrDefault(d => d.TenDichVu == "Gọi món tại phòng");
                        if (roomService == null)
                        {
                            roomService = new DichVu { TenDichVu = "Gọi món tại phòng", Gia = 0, DonVi = "đơn", HoatDong = true };
                            db.DichVus.Add(roomService);
                            db.SaveChanges();
                        }
                        var line = new DatPhongDichVu { BookingId = roomBooking.BookingId, MaDichVu = roomService.MaDichVu,
                            SoLuong = 1, DonGia = totalAmount, ThanhTien = totalAmount };
                        db.DatPhongDichVus.Add(line);
                        roomBooking.TotalAmount += totalAmount;
                        db.SaveChanges();
                        HotelOperations.SynchronizeOutstandingPayment(db, roomBooking);
                        db.Database.ExecuteSqlCommand("UPDATE dbo.DonDatMon SET PayWithBooking=1, BookingServiceId=@p0 WHERE MaDon=@p1", line.MaDatPhongDichVu, orderId);
                    }
                    transaction.Commit();


                    // =================================================
                    // TRẢ KẾT QUẢ
                    // =================================================

                    return Json(new
                    {
                        success = true,

                        orderId =
                            orderId,

                        totalAmount =
                            totalAmount,

                        message =
                            "Đặt món thành công."
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    System.Diagnostics.Trace.TraceError(ex.ToString());


                    return Json(new
                    {
                        success = false,

                        message =
                            "Không thể lưu đơn món. Vui lòng kiểm tra dữ liệu hoặc thử lại sau."
                    });
                }
            }
        }

        // =========================================================
        // ĐẶT MÓN GIAO LÊN PHÒNG VÀ LIÊN KẾT HÓA ĐƠN PHÒNG
        // =========================================================

        [HttpPost]
        public JsonResult PlaceRoomServiceOrder(RestaurantOrderRequest request)
        {
            int userId;
            if (request == null || !int.TryParse(Convert.ToString(Session["UserId"]), out userId))
            { Response.StatusCode = 401; return Json(new { success = false, message = "Vui lòng đăng nhập để gọi món vào hóa đơn phòng." }); }
            var ids = db.Database.SqlQuery<int>("SELECT BookingId FROM dbo.Bookings WHERE UserId=@p0", userId).ToList();
            DateTime today = DateTime.Today;
            string requestedRoom = (request.SoPhong ?? "").Trim();
            var candidates = db.Bookings.AsNoTracking().Include(b => b.Room).Where(b => ids.Contains(b.BookingId) &&
                b.Status == "Đang ở" && b.CheckInDate <= today && b.CheckOutDate > today).ToList()
                .Where(b => (!request.BookingId.HasValue || b.BookingId == request.BookingId.Value) &&
                    (PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, requestedRoom) ||
                     (b.Room != null && string.Equals(b.Room.RoomName, requestedRoom, StringComparison.OrdinalIgnoreCase)))).ToList();
            if (candidates.Count != 1)
                return Json(new { success = false, message = "Vui lòng chọn đúng số phòng đang lưu trú của bạn." });
            var booking = candidates[0];
            if (!PhysicalRoomHelper.IsBookingAssignedToRoom(booking.SpecialRequest, requestedRoom))
            {
                var numbers = HotelOperations.AssignedNumbers(booking.SpecialRequest);
                if (numbers.Count != 1) return Json(new { success = false, message = "Đơn có nhiều phòng. Vui lòng chọn số phòng nhận món cụ thể." });
                request.SoPhong = numbers[0];
            }
            request.BookingId = booking.BookingId;
            request.HoTen = booking.CustomerName;
            request.SoDienThoai = booking.CustomerPhone;
            request.HinhThuc = "Giao lên phòng";
            request.ChargeToRoom = true;
            return PlaceOrder(request);
        }
        // =========================================================
        // LỊCH SỬ GỌI MÓN CỦA KHÁCH
        // =========================================================

        [HttpGet]
        public ActionResult History()
        {
            // =====================================================
            // 1. PHẢI ĐĂNG NHẬP
            // =====================================================

            if (Session["UserId"] == null)
            {
                return RedirectToAction(
                    "Index",
                    "Home",
                    new
                    {
                        loginRequired = 1
                    }
                );
            }


            int userId;


            if (
                !int.TryParse(
                    Session["UserId"].ToString(),
                    out userId
                )
            )
            {
                Session.Remove(
                    "UserId"
                );


                return RedirectToAction(
                    "Index",
                    "Home",
                    new
                    {
                        loginRequired = 1
                    }
                );
            }


            // =====================================================
            // 2. LẤY ĐƠN
            // =====================================================

            List<RestaurantOrderHistoryViewModel> orders =
                db.Database
                  .SqlQuery<RestaurantOrderHistoryViewModel>(
                      @"
                SELECT
                    MaDon,
                    HoTen,
                    SoDienThoai,
                    HinhThuc,
                    SoBan,
                    SoPhong,
                    GhiChu,
                    TongTien,
                    TrangThai,
                    PhuongThucThanhToan,
                    TrangThaiThanhToan,
                    CreatedAt

                FROM dbo.DonDatMon

                WHERE UserId = @p0

                ORDER BY
                    CreatedAt DESC,
                    MaDon DESC
              ",
                      userId
                  )
                  .ToList();


            // =====================================================
            // 3. LẤY CHI TIẾT TỪNG ĐƠN
            // =====================================================

            foreach (
                RestaurantOrderHistoryViewModel order
                in orders
            )
            {
                order.Items =
                    db.Database
                      .SqlQuery<RestaurantOrderHistoryItemViewModel>(
                          @"
                    SELECT
                        m.TenMonAn,
                        ct.SoLuong,
                        ct.DonGia,
                        ct.ThanhTien

                    FROM dbo.ChiTietDonDatMon AS ct

                    INNER JOIN dbo.MonAn AS m
                        ON ct.MaMonAn =
                           m.MaMonAn

                    WHERE
                        ct.MaDon = @p0

                    ORDER BY
                        ct.MaChiTiet
                  ",
                          order.MaDon
                      )
                      .ToList();
            }


            return View(
                orders
            );
        }
        // =========================================================
        // HỦY ĐƠN MÓN
        // Chỉ được hủy khi còn Chờ xác nhận
        // =========================================================

        [HttpPost]
        public JsonResult CancelOrder(
            int id
        )
        {
            int userId;
            if (!int.TryParse(Convert.ToString(Session["UserId"]), out userId)) { Response.StatusCode = 401; return Json(new { success = false, message = "Vui lòng đăng nhập lại." }); }
            var order = HotelOperations.FoodOrder(db, id);
            if (order == null || order.UserId != userId || order.TrangThai != "Chờ xác nhận")
                return Json(new { success = false, message = "Đơn không thuộc tài khoản hoặc không thể hủy." });
            try { HotelOperations.ChangeFoodStatus(db, id, "Đã hủy"); }
            catch (HotelRuleException ex) { return Json(new { success = false, message = ex.Message }); }
            return Json(new { success = true, message = "Đã hủy đơn món, giữ nguyên lịch sử." });
        }
        // =========================================================
        // QUẢN LÝ ĐƠN GỌI MÓN - ADMIN / STAFF
        // =========================================================

        [HttpGet]
        public ActionResult AdminOrders()
        {
            // =====================================================
            // KIỂM TRA ĐĂNG NHẬP
            // =====================================================

            if (Session["UserId"] == null)
            {
                return RedirectToAction(
                    "Index",
                    "Home",
                    new
                    {
                        loginRequired = 1
                    }
                );
            }


            string role =
                Session["Role"] != null
                ? Session["Role"].ToString()
                : "";


            if (
                role != "Admin" &&
                role != "Staff"
            )
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            // =====================================================
            // LẤY TOÀN BỘ ĐƠN
            // =====================================================

            List<RestaurantOrderHistoryViewModel> orders =
                db.Database
                  .SqlQuery<RestaurantOrderHistoryViewModel>(
                      @"
                SELECT
                    MaDon,
                    HoTen,
                    SoDienThoai,
                    HinhThuc,
                    SoBan,
                    SoPhong,
                    GhiChu,
                    TongTien,
                    TrangThai,
                    PhuongThucThanhToan,
                    TrangThaiThanhToan,
                    CreatedAt

                FROM dbo.DonDatMon

                ORDER BY
                    CASE
                        WHEN TrangThai = N'Chờ xác nhận'
                            THEN 1

                        WHEN TrangThai = N'Đã xác nhận'
                            THEN 2

                        WHEN TrangThai = N'Đang chuẩn bị'
                            THEN 3

                        ELSE 4
                    END,

                    CreatedAt DESC
              "
                  )
                  .ToList();


            // =====================================================
            // LẤY MÓN TRONG TỪNG ĐƠN
            // =====================================================

            foreach (
                RestaurantOrderHistoryViewModel order
                in orders
            )
            {
                order.Items =
                    db.Database
                      .SqlQuery<RestaurantOrderHistoryItemViewModel>(
                          @"
                    SELECT
                        m.TenMonAn,
                        ct.SoLuong,
                        ct.DonGia,
                        ct.ThanhTien

                    FROM dbo.ChiTietDonDatMon AS ct

                    INNER JOIN dbo.MonAn AS m
                        ON ct.MaMonAn =
                           m.MaMonAn

                    WHERE
                        ct.MaDon = @p0

                    ORDER BY
                        ct.MaChiTiet
                  ",
                          order.MaDon
                      )
                      .ToList();
            }


            return View(
                orders
            );
        }


        // =========================================================
        // CẬP NHẬT TRẠNG THÁI ĐƠN MÓN
        // =========================================================

        [HttpPost]
        public JsonResult UpdateOrderStatus(
            int id,
            string status
        )
        {
            try { HotelOperations.ChangeFoodStatus(db, id, (status ?? "").Trim()); }
            catch (HotelRuleException ex) { return Json(new { success = false, message = ex.Message }); }
            return Json(new { success = true, message = "Đã cập nhật trạng thái đơn món." });
        }
        // =========================================================
        // CẬP NHẬT THANH TOÁN ĐƠN MÓN
        // =========================================================

        [HttpPost]
        public JsonResult UpdateOrderPayment(
            int id,
            string status
        )
        {
            if (status != "Đã thanh toán") return Json(new { success = false, message = "Không thể đảo thanh toán. Cần quy trình hoàn tiền riêng." });
            try { HotelOperations.ConfirmFoodPayment(db, id, Request.Headers["X-Payment-Evidence"] ?? Request["paymentEvidence"], Convert.ToInt32(Session["UserId"])); }
            catch (HotelRuleException ex) { return Json(new { success = false, message = ex.Message }); }
            return Json(new { success = true, message = "Đã xác minh và ghi nhận thanh toán." });
        }
        // =========================================================
        // ADMIN - QUẢN LÝ THỰC ĐƠN
        // =========================================================

        [HttpGet]
        public ActionResult ManageFoods()
        {
            string role =
                Session["Role"] != null
                ? Session["Role"].ToString()
                : "";


            if (role != "Admin")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            List<RestaurantFoodAdminViewModel> foods =
                db.Database
                  .SqlQuery<RestaurantFoodAdminViewModel>(
                      @"
                SELECT
                    m.MaMonAn,
                    m.MaDanhMuc,
                    d.TenDanhMuc,
                    m.TenMonAn,
                    m.MoTa,
                    m.Gia,
                    m.HinhAnh,
                    m.DonVi,
                    m.ThoiGianChuanBi,
                    m.NoiBat,
                    m.HoatDong

                FROM dbo.MonAn AS m

                INNER JOIN dbo.DanhMucMonAn AS d
                    ON m.MaDanhMuc =
                       d.MaDanhMuc

                ORDER BY
                    d.ThuTu,
                    m.MaMonAn DESC
              "
                  )
                  .ToList();

            foreach (var f in foods)
            {
                f.HinhAnh = GetSmartFoodImage(f.TenMonAn, f.HinhAnh);
            }


            return View(
                foods
            );
        }
        // =========================================================
        // ADMIN - BẬT / NGỪNG BÁN MÓN
        // =========================================================

        [HttpPost]
        public JsonResult ToggleFoodStatus(
            int id
        )
        {
            string role =
                Session["Role"] != null
                ? Session["Role"].ToString()
                : "";


            if (role != "Admin")
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Bạn không có quyền thực hiện chức năng này."
                });
            }


            int affected =
                db.Database.ExecuteSqlCommand(
                    @"
                UPDATE dbo.MonAn

                SET HoatDong =
                    CASE
                        WHEN HoatDong = 1
                            THEN 0
                        ELSE 1
                    END

                WHERE MaMonAn = @p0
            ",
                    id
                );


            if (affected == 0)
            {
                return Json(new
                {
                    success = false,

                    message =
                        "Không tìm thấy món ăn."
                });
            }


            return Json(new
            {
                success = true,

                message =
                    "Đã cập nhật trạng thái món."
            });
        }
        // =========================================================
        // ADMIN - THÊM MÓN
        // =========================================================

        [HttpGet]
        public ActionResult AddFood()
        {
            string role =
                Session["Role"] != null
                ? Session["Role"].ToString()
                : "";


            if (role != "Admin")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            ViewBag.Categories =
                db.Database
                  .SqlQuery<RestaurantCategoryViewModel>(
                      @"
                SELECT
                    MaDanhMuc,
                    TenDanhMuc,
                    MoTa,
                    ThuTu

                FROM dbo.DanhMucMonAn

                WHERE HoatDong = 1

                ORDER BY ThuTu
              "
                  )
                  .ToList();


            return View();
        }


        [HttpPost]
        public ActionResult AddFood(
            int maDanhMuc,
            string tenMonAn,
            string moTa,
            decimal gia,
HttpPostedFileBase hinhAnhFile, string donVi,
            int? thoiGianChuanBi,
            bool noiBat = false
        )
        {
            string role =
                Session["Role"] != null
                ? Session["Role"].ToString()
                : "";


            if (role != "Admin")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            tenMonAn =
                (tenMonAn ?? "").Trim();


            moTa =
                string.IsNullOrWhiteSpace(moTa)
                ? null
                : moTa.Trim();


            string hinhAnh =
    null;


            // =====================================================
            // XỬ LÝ ẢNH MÓN
            // =====================================================

            if (
                hinhAnhFile != null &&
                hinhAnhFile.ContentLength > 0
            )
            {
                // Giới hạn 2MB
                if (
                    hinhAnhFile.ContentLength >
                    2 * 1024 * 1024
                )
                {
                    FailOperation(); TempData["FoodError"] =
                        "Ảnh món không được vượt quá 2MB.";

                    return RedirectToAction(
                        "AddFood"
                    );
                }


                string extension =
                    Path.GetExtension(
                        hinhAnhFile.FileName
                    )
                    .ToLower();


                string[] allowedExtensions =
                {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };


                if (
                    !allowedExtensions.Contains(
                        extension
                    )
                )
                {
                    FailOperation(); TempData["FoodError"] =
                        "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP.";

                    return RedirectToAction(
                        "AddFood"
                    );
                }


                string fileName =
                    Guid.NewGuid()
                        .ToString("N")
                    +
                    extension;


                string folderPath =
                    Server.MapPath(
                        "~/Images/Foods/"
                    );


                if (
                    !Directory.Exists(
                        folderPath
                    )
                )
                {
                    Directory.CreateDirectory(
                        folderPath
                    );
                }


                string fullPath =
                    Path.Combine(
                        folderPath,
                        fileName
                    );


                hinhAnhFile.SaveAs(
                    fullPath
                );


                hinhAnh =
                    "/Images/Foods/"
                    +
                    fileName;
            }


            donVi =
                string.IsNullOrWhiteSpace(donVi)
                ? "phần"
                : donVi.Trim();


            if (
                string.IsNullOrWhiteSpace(
                    tenMonAn
                )
            )
            {
                FailOperation(); TempData["FoodError"] =
                    "Vui lòng nhập tên món.";

                return RedirectToAction(
                    "AddFood"
                );
            }


            if (gia <= 0)
            {
                FailOperation(); TempData["FoodError"] =
                    "Giá món phải lớn hơn 0.";

                return RedirectToAction(
                    "AddFood"
                );
            }


            int categoryExists =
                db.Database
                  .SqlQuery<int>(
                      @"
                SELECT COUNT(*)

                FROM dbo.DanhMucMonAn

                WHERE
                    MaDanhMuc = @p0
                    AND HoatDong = 1
              ",
                      maDanhMuc
                  )
                  .First();


            if (categoryExists == 0)
            {
                FailOperation(); TempData["FoodError"] =
                    "Danh mục món ăn không hợp lệ.";

                return RedirectToAction(
                    "AddFood"
                );
            }


            db.Database.ExecuteSqlCommand(
                @"
            INSERT INTO dbo.MonAn
            (
                MaDanhMuc,
                TenMonAn,
                MoTa,
                Gia,
                HinhAnh,
                DonVi,
                ThoiGianChuanBi,
                NoiBat,
                HoatDong
            )

            VALUES
            (
                @p0,
                @p1,
                @p2,
                @p3,
                @p4,
                @p5,
                @p6,
                @p7,
                1
            )
        ",
                maDanhMuc,
                tenMonAn,
                (object)moTa ?? DBNull.Value,
                gia,
                (object)hinhAnh ?? DBNull.Value,
                donVi,
                (object)thoiGianChuanBi ??
                    DBNull.Value,
                noiBat
            );


            TempData["FoodSuccess"] =
                "Thêm món ăn thành công.";


            return RedirectToAction(
                "ManageFoods"
            );
        }
        // =========================================================
        // ADMIN - SỬA MÓN
        // =========================================================

        [HttpGet]
        public ActionResult EditFood(
            int id
        )
        {
            string role =
                Session["Role"] != null
                ? Session["Role"].ToString()
                : "";


            if (role != "Admin")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            RestaurantFoodAdminViewModel food =
                db.Database
                  .SqlQuery<RestaurantFoodAdminViewModel>(
                      @"
                SELECT TOP 1
                    m.MaMonAn,
                    m.MaDanhMuc,
                    d.TenDanhMuc,
                    m.TenMonAn,
                    m.MoTa,
                    m.Gia,
                    m.HinhAnh,
                    m.DonVi,
                    m.ThoiGianChuanBi,
                    m.NoiBat,
                    m.HoatDong

                FROM dbo.MonAn AS m

                INNER JOIN dbo.DanhMucMonAn AS d
                    ON m.MaDanhMuc =
                       d.MaDanhMuc

                WHERE
                    m.MaMonAn = @p0
              ",
                      id
                  )
                  .FirstOrDefault();


            if (food == null)
            {
                return HttpNotFound();
            }


            ViewBag.Categories =
                db.Database
                  .SqlQuery<RestaurantCategoryViewModel>(
                      @"
                SELECT
                    MaDanhMuc,
                    TenDanhMuc,
                    MoTa,
                    ThuTu

                FROM dbo.DanhMucMonAn

                WHERE HoatDong = 1

                ORDER BY ThuTu
              "
                  )
                  .ToList();


            return View(
                food
            );
        }


        [HttpPost]
        public ActionResult EditFood(
            int maMonAn,
            int maDanhMuc,
            string tenMonAn,
            string moTa,
            decimal gia,
string hinhAnhCu,
HttpPostedFileBase hinhAnhFile, string donVi,
            int? thoiGianChuanBi,
            bool noiBat = false,
            bool hoatDong = false
        )
        {
            string role =
                Session["Role"] != null
                ? Session["Role"].ToString()
                : "";


            if (role != "Admin")
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            tenMonAn =
                (tenMonAn ?? "").Trim();


            if (
                string.IsNullOrWhiteSpace(
                    tenMonAn
                )
            )
            {
                FailOperation(); TempData["FoodError"] =
                    "Tên món không được để trống.";

                return RedirectToAction(
                    "EditFood",
                    new
                    {
                        id = maMonAn
                    }
                );
            }


            if (gia <= 0)
            {
                FailOperation(); TempData["FoodError"] =
                    "Giá món phải lớn hơn 0.";

                return RedirectToAction(
                    "EditFood",
                    new
                    {
                        id = maMonAn
                    }
                );
            }


            moTa =
                string.IsNullOrWhiteSpace(moTa)
                ? null
                : moTa.Trim();


            string hinhAnh =
    string.IsNullOrWhiteSpace(
        hinhAnhCu
    )
    ? null
    : hinhAnhCu.Trim();


            // =====================================================
            // NẾU ADMIN CHỌN ẢNH MỚI
            // =====================================================

            if (
                hinhAnhFile != null &&
                hinhAnhFile.ContentLength > 0
            )
            {
                if (
                    hinhAnhFile.ContentLength >
                    2 * 1024 * 1024
                )
                {
                    FailOperation(); TempData["FoodError"] =
                        "Ảnh món không được vượt quá 2MB.";

                    return RedirectToAction(
                        "EditFood",
                        new
                        {
                            id = maMonAn
                        }
                    );
                }


                string extension =
                    Path.GetExtension(
                        hinhAnhFile.FileName
                    )
                    .ToLower();


                string[] allowedExtensions =
                {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };


                if (
                    !allowedExtensions.Contains(
                        extension
                    )
                )
                {
                    FailOperation(); TempData["FoodError"] =
                        "Chỉ chấp nhận JPG, JPEG, PNG hoặc WEBP.";

                    return RedirectToAction(
                        "EditFood",
                        new
                        {
                            id = maMonAn
                        }
                    );
                }


                string fileName =
                    Guid.NewGuid()
                        .ToString("N")
                    +
                    extension;


                string folderPath =
                    Server.MapPath(
                        "~/Images/Foods/"
                    );


                if (
                    !Directory.Exists(
                        folderPath
                    )
                )
                {
                    Directory.CreateDirectory(
                        folderPath
                    );
                }


                hinhAnhFile.SaveAs(
                    Path.Combine(
                        folderPath,
                        fileName
                    )
                );


                hinhAnh =
                    "/Images/Foods/"
                    +
                    fileName;
            }


            donVi =
                string.IsNullOrWhiteSpace(donVi)
                ? "phần"
                : donVi.Trim();


            int affected =
                db.Database.ExecuteSqlCommand(
                    @"
                UPDATE dbo.MonAn

                SET
                    MaDanhMuc = @p0,
                    TenMonAn = @p1,
                    MoTa = @p2,
                    Gia = @p3,
                    HinhAnh = @p4,
                    DonVi = @p5,
                    ThoiGianChuanBi = @p6,
                    NoiBat = @p7,
                    HoatDong = @p8

                WHERE
                    MaMonAn = @p9
            ",
                    maDanhMuc,
                    tenMonAn,
                    (object)moTa ??
                        DBNull.Value,
                    gia,
                    (object)hinhAnh ??
                        DBNull.Value,
                    donVi,
                    (object)thoiGianChuanBi ??
                        DBNull.Value,
                    noiBat,
                    hoatDong,
                    maMonAn
                );


            if (affected == 0)
            {
                return HttpNotFound();
            }


            TempData["FoodSuccess"] =
                "Cập nhật món ăn thành công.";


            return RedirectToAction(
                "ManageFoods"
            );
        }

        private string GetSmartFoodImage(string tenMonAn, string currentHinhAnh)
        {
            if (!string.IsNullOrWhiteSpace(currentHinhAnh) && (currentHinhAnh.StartsWith("http") || currentHinhAnh.StartsWith("/")))
            {
                return currentHinhAnh;
            }

            string name = (tenMonAn ?? "").ToLowerInvariant();

            if (name.Contains("salad") || name.Contains("rau"))
                return "https://images.unsplash.com/photo-1540420773420-3366772f4999?auto=format&fit=crop&w=800&q=80";
            if (name.Contains("bò") || name.Contains("steak") || name.Contains("bít tết"))
                return "https://images.unsplash.com/photo-1544025162-d76694265947?auto=format&fit=crop&w=800&q=80";
            if (name.Contains("cá hồi") || name.Contains("salmon"))
                return "https://images.unsplash.com/photo-1519708227418-c8fd9a32b7a2?auto=format&fit=crop&w=800&q=80";
            if (name.Contains("phở") || name.Contains("mì") || name.Contains("bún") || name.Contains("hủ tiếu") || name.Contains("soup") || name.Contains("súp"))
                return "https://images.unsplash.com/photo-1569718212165-3a8278d5f624?auto=format&fit=crop&w=800&q=80";
            if (name.Contains("gà") || name.Contains("chicken"))
                return "https://images.unsplash.com/photo-1626082927389-6cd097cdc6ec?auto=format&fit=crop&w=800&q=80";
            if (name.Contains("cơm") || name.Contains("rice"))
                return "https://images.unsplash.com/photo-1512058564366-18510be2db19?auto=format&fit=crop&w=800&q=80";
            if (name.Contains("tôm") || name.Contains("hải sản") || name.Contains("cua") || name.Contains("mực"))
                return "https://images.unsplash.com/photo-1565557623262-b51c2513a641?auto=format&fit=crop&w=800&q=80";
            if (name.Contains("pizza") || name.Contains("burger"))
                return "https://images.unsplash.com/photo-1513104890138-7c749659a591?auto=format&fit=crop&w=800&q=80";
            if (name.Contains("bánh") || name.Contains("chè") || name.Contains("kem") || name.Contains("dessert") || name.Contains("cake"))
                return "https://images.unsplash.com/photo-1551024709-8f23befc6f87?auto=format&fit=crop&w=800&q=80";
            if (name.Contains("nước") || name.Contains("sinh tố") || name.Contains("nước ép") || name.Contains("cocktail") || name.Contains("rượu") || name.Contains("trà") || name.Contains("coffee") || name.Contains("cà phê"))
                return "https://images.unsplash.com/photo-1541658016709-82535e94bc69?auto=format&fit=crop&w=800&q=80";

            return "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=800&q=80";
        }

        [HttpGet]
        public JsonResult GetMyActiveRooms()
        {
            int userId;
            if (!int.TryParse(Convert.ToString(Session["UserId"]), out userId))
            {
                Response.StatusCode = 401;
                return Json(new { success = false, message = "Vui lòng đăng nhập lại." }, JsonRequestBehavior.AllowGet);
            }
            DateTime today = DateTime.Today;
            var ids = db.Database.SqlQuery<int>("SELECT BookingId FROM dbo.Bookings WHERE UserId=@p0", userId).ToList();
            var bookings = db.Bookings.AsNoTracking().Include(b => b.Room).Where(b => ids.Contains(b.BookingId) &&
                b.Status == "Đang ở" && b.CheckInDate <= today && b.CheckOutDate > today).ToList();
            var physical = db.PhysicalRooms.AsNoTracking().Where(p => p.IsActive).ToList();
            var rooms = bookings.SelectMany(b => physical.Where(p => p.RoomTypeId == b.RoomId &&
                PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, p.RoomNumber.Trim()))
                .Select(p => new { bookingId = b.BookingId, roomId = b.RoomId, roomNumber = p.RoomNumber,
                    roomName = b.Room.RoomName, roomType = b.Room.RoomType, status = b.Status,
                    checkIn = b.CheckInDate.ToString("dd/MM/yyyy"), checkOut = b.CheckOutDate.ToString("dd/MM/yyyy") })).ToList();
            return Json(new { success = true, rooms }, JsonRequestBehavior.AllowGet);
        }



        protected override void Dispose(
            bool disposing
        )
        {
            if (disposing)
            {
                db.Dispose();
            }


            base.Dispose(
                disposing
            );
        }
    }
}
