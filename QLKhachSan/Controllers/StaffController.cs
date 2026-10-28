using QLKhachSan.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace QLKhachSan.Controllers
{
    public class StaffController : Controller
    {
        private readonly HotelDbContext db =
            new HotelDbContext();


        // =====================================================
        // KIỂM TRA QUYỀN NHÂN VIÊN
        // =====================================================

        private bool IsStaff()
        {
            return
                Session["UserId"] != null &&
                Session["Role"] != null &&
                Session["Role"].ToString() == "Staff";
        }


        // =====================================================
        // TRANG CHÍNH NHÂN VIÊN
        // =====================================================

        public ActionResult Index()
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            DateTime today =
                DateTime.Today;

            DateTime tomorrow =
                today.AddDays(1);


            // =====================================================
            // 1. ĐƠN ĐẶT PHÒNG ĐANG CHỜ XÁC NHẬN
            // =====================================================

            ViewBag.PendingBookings =
                db.Bookings.Count(
                    b =>
                    b.Status ==
                    "Chờ xác nhận"
                );


            // =====================================================
            // 2. KHÁCH NHẬN PHÒNG HÔM NAY
            // =====================================================

            ViewBag.TodayCheckIns =
                db.Bookings.Count(
                    b =>
                    b.Status ==
                    "Đã xác nhận"
                    &&
                    b.CheckInDate >= today
                    &&
                    b.CheckInDate < tomorrow
                );


            // =====================================================
            // 3. ĐƠN GỌI MÓN ĐANG CHỜ XỬ LÝ
            // =====================================================

            ViewBag.PendingFoodOrders =
                db.Database
                  .SqlQuery<int>(
                      @"
                SELECT COUNT(*)

                FROM dbo.DonDatMon

                WHERE
                    TrangThai IN
                    (
                        N'Chờ xác nhận',
                        N'Đã xác nhận',
                        N'Đang chuẩn bị',
                        N'Đang phục vụ',
                        N'Đang giao'
                    )
              "
                  )
                  .FirstOrDefault();


            // =====================================================
            // 4. DOANH THU PHÒNG HÔM NAY
            // CHỈ TÍNH THANH TOÁN ĐÃ XÁC NHẬN
            // =====================================================

            decimal roomRevenueToday =
                db.ThanhToans
                  .Where(
                      t =>
                      t.TrangThai ==
                      "Đã thanh toán"
                      &&
                      t.NgayThanhToan >= today
                      &&
                      t.NgayThanhToan < tomorrow
                  )
                  .Select(
                      t =>
                      (decimal?)t.SoTien
                  )
                  .Sum()
                ?? 0;


            // =====================================================
            // 5. DOANH THU GỌI MÓN HÔM NAY
            // =====================================================

            decimal foodRevenueToday =
                db.Database
                  .SqlQuery<decimal>(
                      @"
                SELECT
                    ISNULL(
                        SUM(TongTien),
                        0
                    )

                FROM dbo.DonDatMon

                WHERE
                    TrangThaiThanhToan =
                        N'Đã thanh toán'

                    AND CreatedAt >= @p0
                    AND CreatedAt < @p1
              ",
                      today,
                      tomorrow
                  )
                  .FirstOrDefault();


            ViewBag.TodayRevenue =
                roomRevenueToday +
                foodRevenueToday;


            // =====================================================
            // 6. TỔNG ĐƠN PHÒNG HÔM NAY
            // =====================================================

            ViewBag.TodayBookings =
                db.Bookings.Count(
                    b =>
                    b.CreatedAt >= today
                    &&
                    b.CreatedAt < tomorrow
                );


            return View();
        }

        // =====================================================
        // DANH SÁCH KHÁCH NHẬN PHÒNG HÔM NAY
        // =====================================================

        public ActionResult TodayCheckIns()

        {

            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            DateTime today =
                DateTime.Today;

            DateTime tomorrow =
                today.AddDays(1);


            var bookings =
                db.Bookings
                  .Include(b => b.Room)
                  .Include(b => b.ThanhToans)
                  .Where(
                      b =>
                      b.Status == "Đã xác nhận"
                      &&
                      b.CheckInDate >= today
                      &&
                      b.CheckInDate < tomorrow
                  )
                  .OrderBy(
                      b => b.CheckInDate
                  )
                  .ThenBy(
                      b => b.CustomerName
                  )
                  .ToList();


            return View(bookings);
        }
        // =====================================================
        // XÁC NHẬN KHÁCH NHẬN PHÒNG
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckInBooking(
            int id
        )
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }

            var booking =
                db.Bookings.Find(id);

            if (booking == null)
            {
                return HttpNotFound();
            }

            if (
                booking.Status !=
                "Đã xác nhận"
            )
            {
                TempData["StaffError"] =
                    "Chỉ có thể nhận phòng đối với đơn đã xác nhận.";

                return RedirectToAction(
                    "TodayCheckIns"
                );
            }

            if (
                booking.CheckInDate.Date !=
                DateTime.Today
            )
            {
                TempData["StaffError"] =
                    "Đơn này không có ngày nhận phòng hôm nay.";

                return RedirectToAction(
                    "TodayCheckIns"
                );
            }

            var payment = db.ThanhToans.FirstOrDefault(p => p.BookingId == id);
            if (payment == null || payment.TrangThai != "Đã thanh toán")
            {
                TempData["StaffError"] =
                    "Đơn đặt phòng chưa hoàn tất thanh toán. Không thể nhận phòng.";

                return RedirectToAction(
                    "TodayCheckIns"
                );
            }

            booking.Status =
                "Đang ở";

            db.SaveChanges();

            TempData["StaffSuccess"] =
                "Đã xác nhận nhận phòng cho đơn #" +
                booking.BookingId +
                ".";

            return RedirectToAction(
                "TodayCheckIns"
            );
        }


        // =====================================================
        // XÁC NHẬN KHÁCH TRẢ PHÒNG
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckOutBooking(
            int id
        )
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }

            var booking =
                db.Bookings.Find(id);

            if (booking == null)
            {
                return HttpNotFound();
            }

            if (
                booking.Status !=
                "Đang ở"
            )
            {
                TempData["StaffError"] =
                    "Chỉ có thể trả phòng đối với khách đang ở.";

                return RedirectToAction(
                    "Bookings"
                );
            }
            bool hasPaid =
    db.ThanhToans.Any(
        t =>
        t.BookingId == id
        &&
        t.TrangThai ==
        "Đã thanh toán"
    );


            if (!hasPaid)
            {
                TempData["StaffError"] =
                    "Khách chưa thanh toán. Vui lòng xác nhận thanh toán trước khi trả phòng.";

                return RedirectToAction(
                    "Bookings"
                );
            }

            booking.Status =
                "Đã trả phòng";

            db.SaveChanges();

            TempData["StaffSuccess"] =
                "Đã xác nhận trả phòng cho đơn #" +
                booking.BookingId +
                ".";

            return RedirectToAction(
                "Bookings"
            );
        }
        // =====================================================
        // DANH SÁCH ĐƠN ĐẶT PHÒNG
        // =====================================================

        public ActionResult Bookings(
            string keyword,
            string status,
            DateTime? fromDate,
            DateTime? toDate
        )
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            var query =
                db.Bookings
                  .Include(b => b.Room)
                  .Include(b => b.ThanhToans)
                  .AsQueryable();


            // =================================================
            // TÌM KHÁCH
            // =================================================

            if (
                !string.IsNullOrWhiteSpace(
                    keyword
                )
            )
            {
                keyword =
                    keyword.Trim();


                query =
                    query.Where(
                        b =>
                        b.CustomerName.Contains(keyword) ||
                        b.CustomerPhone.Contains(keyword) ||
                        b.CustomerEmail.Contains(keyword)
                    );
            }


            // =================================================
            // LỌC TRẠNG THÁI
            // =================================================

            if (
                !string.IsNullOrWhiteSpace(
                    status
                )
            )
            {
                query =
                    query.Where(
                        b =>
                        b.Status == status
                    );
            }


            // =================================================
            // TỪ NGÀY
            // =================================================

            if (fromDate.HasValue)
            {
                DateTime startDate =
                    fromDate.Value.Date;


                query =
                    query.Where(
                        b =>
                        b.CreatedAt >= startDate
                    );
            }


            // =================================================
            // ĐẾN NGÀY
            // =================================================

            if (toDate.HasValue)
            {
                DateTime endDate =
                    toDate.Value
                          .Date
                          .AddDays(1);


                query =
                    query.Where(
                        b =>
                        b.CreatedAt < endDate
                    );
            }


            var bookings =
                query
                    .OrderByDescending(
                        b => b.CreatedAt
                    )
                    .ToList();


            // =================================================
            // GIỮ GIÁ TRỊ BỘ LỌC
            // =================================================

            ViewBag.Keyword =
                keyword;

            ViewBag.Status =
                status;

            ViewBag.FromDate =
                fromDate.HasValue
                ? fromDate.Value.ToString(
                    "yyyy-MM-dd"
                  )
                : "";

            ViewBag.ToDate =
                toDate.HasValue
                ? toDate.Value.ToString(
                    "yyyy-MM-dd"
                  )
                : "";


            // =================================================
            // THỐNG KÊ NHANH
            // =================================================

            ViewBag.TotalBookings =
                db.Bookings.Count();


            ViewBag.PendingBookings =
                db.Bookings.Count(
                    b =>
                    b.Status ==
                    "Chờ xác nhận"
                );


            ViewBag.ConfirmedBookings =
                db.Bookings.Count(
                    b =>
                    b.Status ==
                    "Đã xác nhận"
                );


            return View(
                bookings
            );
        }


        // =====================================================
        // XÁC NHẬN ĐƠN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmBooking(
            int id
        )
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            var booking =
                db.Bookings.Find(id);


            if (booking == null)
            {
                return HttpNotFound();
            }


            if (
    booking.Status !=
    "Chờ xác nhận"
)
            {
                TempData["StaffError"] =
                    "Chỉ có thể xác nhận đơn đang chờ xác nhận.";

                return RedirectToAction(
                    "Bookings"
                );
            }


            booking.Status =
                "Đã xác nhận";


            db.SaveChanges();


            TempData["StaffSuccess"] =
                "Đã xác nhận đơn #" +
                booking.BookingId +
                ".";


            return RedirectToAction(
                "Bookings"
            );
        }


        // =====================================================
        // HỦY ĐƠN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelBooking(
            int id
        )
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            var booking =
                db.Bookings.Find(id);


            if (booking == null)
            {
                return HttpNotFound();
            }

            if (
    booking.Status !=
    "Chờ xác nhận"
    &&
    booking.Status !=
    "Đã xác nhận"
)
            {
                TempData["StaffError"] =
                    "Chỉ có thể hủy đơn trước khi khách nhận phòng.";

                return RedirectToAction(
                    "Bookings"
                );
            }
            // =================================================
            // KHÔNG HỦY ĐƠN ĐÃ THANH TOÁN
            // =================================================

            bool hasPaid =
                db.ThanhToans.Any(
                    t =>
                    t.BookingId == id &&
                    t.TrangThai ==
                    "Đã thanh toán"
                );


            if (hasPaid)
            {
                TempData["StaffError"] =
                    "Đơn đã thanh toán. Không thể hủy trực tiếp.";

                return RedirectToAction(
                    "Bookings"
                );
            }


            booking.Status =
                "Đã hủy";


            // =================================================
            // HỦY CÁC THANH TOÁN CHƯA HOÀN THÀNH
            // =================================================

            var payments =
                db.ThanhToans
                  .Where(
                      t =>
                      t.BookingId == id
                  )
                  .ToList();


            foreach (
                var payment
                in payments
            )
            {
                if (
                    payment.TrangThai !=
                    "Đã thanh toán"
                )
                {
                    payment.TrangThai =
                        "Đã hủy";
                }
            }


            db.SaveChanges();


            TempData["StaffSuccess"] =
                "Đã hủy đơn #" +
                booking.BookingId +
                ".";


            return RedirectToAction(
                "Bookings"
            );
        }


        // =====================================================
        // XÁC NHẬN THANH TOÁN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmPayment(
            int id
        )
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            var booking =
                db.Bookings.Find(id);


            if (booking == null)
            {
                return HttpNotFound();
            }


            if (
                booking.Status ==
                "Đã hủy"
            )
            {
                TempData["StaffError"] =
                    "Không thể xác nhận thanh toán cho đơn đã hủy.";

                return RedirectToAction(
                    "Bookings"
                );
            }

            if (
    booking.Status !=
    "Đã xác nhận"
    &&
    booking.Status !=
    "Đang ở"
)
            {
                TempData["StaffError"] =
                    "Trạng thái đơn hiện tại không thể xác nhận thanh toán.";

                return RedirectToAction(
                    "Bookings"
                );
            }
            var payment =
                db.ThanhToans
                  .Where(
                      t =>
                      t.BookingId == id
                  )
                  .OrderByDescending(
                      t =>
                      t.NgayThanhToan
                  )
                  .FirstOrDefault();
            if (
    payment != null
    &&
    payment.TrangThai ==
    "Đã thanh toán"
)
            {
                TempData["StaffError"] =
                    "Đơn này đã được thanh toán trước đó.";

                return RedirectToAction(
                    "Bookings"
                );
            }


            // =================================================
            // TRƯỜNG HỢP BOOKING CŨ CHƯA CÓ THANH TOÁN
            // =================================================

            if (payment == null)
            {
                payment =
                    new ThanhToan
                    {
                        BookingId =
                            booking.BookingId,

                        PhuongThucThanhToan =
                            booking.PaymentMethod,

                        SoTien =
                            booking.TotalAmount,

                        NgayThanhToan =
                            DateTime.Now,

                        TrangThai =
                            "Đã thanh toán",

                        MaGiaoDich =
                            null,

                        GhiChu =
                            "Nhân viên xác nhận thanh toán"
                    };


                db.ThanhToans.Add(
                    payment
                );
            }
            else
            {
                payment.TrangThai =
                    "Đã thanh toán";

                payment.NgayThanhToan =
                    DateTime.Now;

                payment.SoTien =
                    booking.TotalAmount;


                if (
                    string.IsNullOrWhiteSpace(
                        payment.GhiChu
                    )
                )
                {
                    payment.GhiChu =
                        "Nhân viên xác nhận thanh toán";
                }
            }


            db.SaveChanges();


            TempData["StaffSuccess"] =
                "Đã xác nhận thanh toán cho đơn #" +
                booking.BookingId +
                ".";


            return RedirectToAction(
                "Bookings"
            );
        }


        // =====================================================
        // GIẢI PHÓNG DATABASE
        // =====================================================
        // =====================================================
        // NHÂN VIÊN - TẠO ĐƠN ĐẶT PHÒNG CHO KHÁCH
        // =====================================================

        [HttpGet]
        public ActionResult CreateBooking()
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            ViewBag.Rooms =
                db.Rooms
                  .Where(
                      r => r.IsActive
                  )
                  .OrderBy(
                      r => r.Price
                  )
                  .ToList();


            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateBooking(
            int roomId,
            string customerName,
            string customerPhone,
            string customerEmail,
            DateTime checkInDate,
            DateTime checkOutDate,
            int guestCount,
            int roomCount,
            string paymentMethod,
            string specialRequest
        )
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            // =================================================
            // CHUẨN HÓA
            // =================================================

            customerName =
                (customerName ?? "").Trim();

            customerPhone =
                (customerPhone ?? "").Trim();

            customerEmail =
                (customerEmail ?? "")
                .Trim()
                .ToLowerInvariant();

            paymentMethod =
                (paymentMethod ?? "").Trim();

            specialRequest =
                string.IsNullOrWhiteSpace(
                    specialRequest
                )
                ? null
                : specialRequest.Trim();


            // =================================================
            // KIỂM TRA CƠ BẢN
            // =================================================

            if (
                string.IsNullOrWhiteSpace(
                    customerName
                ) ||
                string.IsNullOrWhiteSpace(
                    customerPhone
                ) ||
                string.IsNullOrWhiteSpace(
                    customerEmail
                )
            )
            {
                TempData["StaffError"] =
                    "Vui lòng nhập đầy đủ thông tin khách hàng.";

                return RedirectToAction(
                    "CreateBooking"
                );
            }


            if (
                checkInDate.Date <
                DateTime.Today
            )
            {
                TempData["StaffError"] =
                    "Ngày nhận phòng không được nhỏ hơn ngày hiện tại.";

                return RedirectToAction(
                    "CreateBooking"
                );
            }


            if (
                checkOutDate.Date <=
                checkInDate.Date
            )
            {
                TempData["StaffError"] =
                    "Ngày trả phòng phải sau ngày nhận phòng.";

                return RedirectToAction(
                    "CreateBooking"
                );
            }


            if (
                guestCount <= 0 ||
                roomCount <= 0
            )
            {
                TempData["StaffError"] =
                    "Số khách và số phòng phải lớn hơn 0.";

                return RedirectToAction(
                    "CreateBooking"
                );
            }


            // =================================================
            // KIỂM TRA PHÒNG
            // =================================================

            var room =
                db.Rooms.FirstOrDefault(
                    r =>
                    r.RoomId == roomId &&
                    r.IsActive
                );


            if (room == null)
            {
                TempData["StaffError"] =
                    "Phòng không tồn tại hoặc đang ngừng hoạt động.";

                return RedirectToAction(
                    "CreateBooking"
                );
            }


            if (
                guestCount >
                room.Capacity * roomCount
            )
            {
                TempData["StaffError"] =
                    "Số khách vượt quá sức chứa của số phòng đã chọn.";

                return RedirectToAction(
                    "CreateBooking"
                );
            }


            // =================================================
            // KIỂM TRA SỐ PHÒNG CÒN TRỐNG
            // =================================================

            int bookedRooms =
                db.Bookings
                  .Where(
                      b =>
                      b.RoomId == roomId &&
                      b.Status != "Đã hủy" &&
                      checkInDate < b.CheckOutDate &&
                      checkOutDate > b.CheckInDate
                  )
                  .Select(
                      b => b.RoomCount
                  )
                  .DefaultIfEmpty(0)
                  .Sum();


            int availableRooms =
                room.TotalRooms -
                bookedRooms;


            if (
                roomCount >
                availableRooms
            )
            {
                TempData["StaffError"] =
                    "Không đủ phòng trống. Hiện chỉ còn "
                    +
                    availableRooms
                    +
                    " phòng.";

                return RedirectToAction(
                    "CreateBooking"
                );
            }


            // =================================================
            // THANH TOÁN
            // =================================================

            if (
                paymentMethod !=
                    "Tiền mặt" &&
                paymentMethod !=
                    "Chuyển khoản"
            )
            {
                TempData["StaffError"] =
                    "Phương thức thanh toán không hợp lệ.";

                return RedirectToAction(
                    "CreateBooking"
                );
            }


            // =================================================
            // TÍNH TIỀN
            // =================================================

            int nights =
                (
                    checkOutDate.Date -
                    checkInDate.Date
                ).Days;


            decimal totalAmount =
                room.Price *
                roomCount *
                nights;


            // =================================================
            // TẠO BOOKING
            // =================================================

            var booking =
                new Booking
                {
                    RoomId =
                        room.RoomId,

                    CustomerName =
                        customerName,

                    CustomerPhone =
                        customerPhone,

                    CustomerEmail =
                        customerEmail,

                    CheckInDate =
                        checkInDate.Date,

                    CheckOutDate =
                        checkOutDate.Date,

                    GuestCount =
                        guestCount,

                    RoomCount =
                        roomCount,

                    SpecialRequest =
                        specialRequest,

                    PaymentMethod =
                        paymentMethod,

                    TotalAmount =
                        totalAmount,

                    Status =
                        "Đã xác nhận",

                    CreatedAt =
                        DateTime.Now
                };


            db.Bookings.Add(
                booking
            );


            db.SaveChanges();


            // =================================================
            // TẠO THANH TOÁN
            // =================================================

            var payment =
                new ThanhToan
                {
                    BookingId =
                        booking.BookingId,

                    PhuongThucThanhToan =
                        paymentMethod,

                    SoTien =
                        totalAmount,

                    NgayThanhToan =
                        DateTime.Now,

                    TrangThai =
                        "Chờ thanh toán",

                    MaGiaoDich =
                        null,

                    GhiChu =
                        "Đơn do nhân viên tạo tại quầy"
                };


            db.ThanhToans.Add(
                payment
            );


            db.SaveChanges();


            TempData["StaffSuccess"] =
                "Tạo đơn #" +
                booking.BookingId +
                " thành công.";


            return RedirectToAction(
                "Bookings"
            );
        }
        // =====================================================
        // NHÂN VIÊN - QUẢN LÝ ĐƠN GỌI MÓN
        // =====================================================

        public ActionResult FoodOrders()
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            var orders =
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

                        WHEN TrangThai = N'Đang phục vụ'
                            THEN 4

                        WHEN TrangThai = N'Đang giao'
                            THEN 5

                        ELSE 6
                    END,

                    CreatedAt DESC
              "
                  )
                  .ToList();


            foreach (
                var order
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
        // =====================================================
        // NHÂN VIÊN - CẬP NHẬT TRẠNG THÁI ĐƠN MÓN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateFoodOrderStatus(
            int id,
            string status
        )
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            status =
                (status ?? "").Trim();


            string[] validStatuses =
            {
        "Chờ xác nhận",
        "Đã xác nhận",
        "Đang chuẩn bị",
        "Đang phục vụ",
        "Đang giao",
        "Hoàn tất",
        "Đã hủy"
    };


            if (
                !validStatuses.Contains(
                    status
                )
            )
            {
                TempData["StaffFoodError"] =
                    "Trạng thái đơn không hợp lệ.";

                return RedirectToAction(
                    "FoodOrders"
                );
            }
            string currentStatus =
    db.Database
      .SqlQuery<string>(
          @"
            SELECT TrangThai
            FROM dbo.DonDatMon
            WHERE MaDon = @p0
          ",
          id
      )
      .FirstOrDefault();


            if (
                string.IsNullOrWhiteSpace(
                    currentStatus
                )
            )
            {
                TempData["StaffFoodError"] =
                    "Không tìm thấy đơn gọi món.";

                return RedirectToAction(
                    "FoodOrders"
                );
            }


            bool canChangeStatus = false;


            if (
                currentStatus ==
                "Chờ xác nhận"
            )
            {
                canChangeStatus =
                    status == "Đã xác nhận"
                    ||
                    status == "Đã hủy";
            }
            else if (
                currentStatus ==
                "Đã xác nhận"
            )
            {
                canChangeStatus =
                    status == "Đang chuẩn bị"
                    ||
                    status == "Đã hủy";
            }
            else if (
                currentStatus ==
                "Đang chuẩn bị"
            )
            {
                canChangeStatus =
                    status == "Đang phục vụ"
                    ||
                    status == "Đang giao"
                    ||
                    status == "Đã hủy";
            }
            else if (
                currentStatus ==
                "Đang phục vụ"
                ||
                currentStatus ==
                "Đang giao"
            )
            {
                canChangeStatus =
                    status == "Hoàn tất";
            }


            if (!canChangeStatus)
            {
                TempData["StaffFoodError"] =
                    "Không thể chuyển đơn từ trạng thái '" +
                    currentStatus +
                    "' sang '" +
                    status +
                    "'.";

                return RedirectToAction(
                    "FoodOrders"
                );
            }

            int affected =
                db.Database.ExecuteSqlCommand(
                    @"
                UPDATE dbo.DonDatMon

                SET TrangThai = @p0

                WHERE MaDon = @p1
            ",
                    status,
                    id
                );


            if (affected == 0)
            {
                TempData["StaffFoodError"] =
                    "Không tìm thấy đơn gọi món.";

                return RedirectToAction(
                    "FoodOrders"
                );
            }


            TempData["StaffFoodSuccess"] =
                "Cập nhật trạng thái đơn #" +
                id +
                " thành công.";


            return RedirectToAction(
                "FoodOrders"
            );
        }
        // =====================================================
        // NHÂN VIÊN - XÁC NHẬN THANH TOÁN ĐƠN MÓN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmFoodPayment(
            int id
        )
        {
            if (!IsStaff())
            {
                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }

            string orderStatus =
    db.Database
      .SqlQuery<string>(
          @"
            SELECT TrangThai
            FROM dbo.DonDatMon
            WHERE MaDon = @p0
          ",
          id
      )
      .FirstOrDefault();


            if (
                string.IsNullOrWhiteSpace(
                    orderStatus
                )
            )
            {
                TempData["StaffFoodError"] =
                    "Không tìm thấy đơn gọi món.";

                return RedirectToAction(
                    "FoodOrders"
                );
            }


            if (
                orderStatus ==
                "Đã hủy"
            )
            {
                TempData["StaffFoodError"] =
                    "Đơn đã hủy nên không thể xác nhận thanh toán.";

                return RedirectToAction(
                    "FoodOrders"
                );
            }


            string currentPaymentStatus =
                db.Database
                  .SqlQuery<string>(
                      @"
            SELECT TrangThaiThanhToan
            FROM dbo.DonDatMon
            WHERE MaDon = @p0
          ",
                      id
                  )
                  .FirstOrDefault();


            if (
                currentPaymentStatus ==
                "Đã thanh toán"
            )
            {
                TempData["StaffFoodError"] =
                    "Đơn gọi món này đã được thanh toán trước đó.";

                return RedirectToAction(
                    "FoodOrders"
                );
            }
            int affected =
                db.Database.ExecuteSqlCommand(
                    @"
                UPDATE dbo.DonDatMon

                SET TrangThaiThanhToan =
                    N'Đã thanh toán'

                WHERE MaDon = @p0
            ",
                    id
                );


            if (affected == 0)
            {
                TempData["StaffFoodError"] =
                    "Không tìm thấy đơn gọi món.";

                return RedirectToAction(
                    "FoodOrders"
                );
            }


            TempData["StaffFoodSuccess"] =
                "Đã xác nhận thanh toán đơn #" +
                id +
                ".";


            return RedirectToAction(
                "FoodOrders"
            );
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