using QLKhachSan.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace QLKhachSan.Controllers
{
    public class StaffController : HotelController
    {
// =====================================================
        // KIỂM TRA QUYỀN NHÂN VIÊN
        // =====================================================

        private bool IsStaff()
        {
            return
                Session["UserId"] != null &&
                Session["Role"] != null &&
                Session["Role"].ToString().Trim().Equals("Staff", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet]
        public ActionResult GetPhysicalRoomsMatrixByDate(string date)
        {
            if (!IsStaff()) return new HttpStatusCodeResult(403);
            if (!HasStaffPermission("rooms.view") && !HasStaffPermission("rooms.manage"))
                return new HttpStatusCodeResult(403);
            bool details = HasStaffPermission("bookings.view") || HasStaffPermission("bookings.approve") ||
                HasStaffPermission("bookings.checkin") || HasStaffPermission("bookings.checkout") || HasStaffPermission("payments.manage");
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            try
            {
                return Json(RoomMatrixService.Load(db, RoomMatrixService.ParseDate(date), details), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Staff room matrix: " + ex);
                Response.StatusCode = 500;
                return Json(new { success = false, message = "Không thể tải lịch phòng. Vui lòng thử lại hoặc liên hệ quản trị viên." }, JsonRequestBehavior.AllowGet);
            }
        }

        private bool HasStaffPermission(string permissionCode)
        {
            if (Session["UserId"] == null) return false;
            string role = Session["Role"]?.ToString()?.Trim();
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase)) return false;

            int userId = Convert.ToInt32(Session["UserId"]);
            var userPerms = Session["Permissions"] as System.Collections.Generic.List<string>;
            if (userPerms == null)
            {
                userPerms = db.UserPermissions.Where(up => up.UserId == userId).Select(up => up.PermissionCode).ToList();
                Session["Permissions"] = userPerms;
            }

            return userPerms != null && userPerms.Contains(permissionCode);
        }

        private bool IsBookingAssignedToRoom(string specialRequest, string targetRoomNumber)
        {
            if (string.IsNullOrEmpty(specialRequest)) return false;
            try
            {
                int idx = 0;
                while ((idx = specialRequest.IndexOf("[Phòng:", idx, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    int start = idx + 7;
                    int end = specialRequest.IndexOf("]", start);
                    if (end > start)
                    {
                        string rNum = specialRequest.Substring(start, end - start).Trim();
                        if (string.Equals(rNum, targetRoomNumber, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    idx = start;
                }
            }
            catch { }
            return false;
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

            int userId = Convert.ToInt32(Session["UserId"]);
            var perms = Session["Permissions"] as System.Collections.Generic.List<string>;
            if (perms == null)
            {
                perms = db.UserPermissions.Where(up => up.UserId == userId).Select(up => up.PermissionCode).ToList();
                Session["Permissions"] = perms;
            }
            ViewBag.Permissions = perms ?? new System.Collections.Generic.List<string>();

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
                    (b.Status == "Đã xác nhận" || b.Status == "Chờ xác nhận")
                    &&
                    DbFunctions.TruncateTime(b.CheckInDate) == today
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
            // 3B. ĐƠN DỊCH VỤ PHÒNG ĐANG CHỜ XỬ LÝ
            // =====================================================

                ViewBag.PendingRoomServices =
                    db.Database
                      .SqlQuery<int>(
                          @"
                    SELECT COUNT(*)

                    FROM dbo.RoomServiceOrders

                    WHERE
                        Status IN
                        (
                            N'Chờ xác nhận',
                            N'Đã xác nhận',
                            N'Đang thực hiện'
                        )
                  "
                      )
                      .FirstOrDefault();



            // =====================================================
            // 4. DOANH THU PHÒNG HÔM NAY
            // CHỈ TÍNH THANH TOÁN ĐÃ XÁC NHẬN
            // =====================================================

            decimal roomRevenueToday =
                HotelOperations.RoomRevenue(db, today, tomorrow);


            // =====================================================
            // 5. DOANH THU GỌI MÓN HÔM NAY
            // =====================================================

            decimal foodRevenueToday = 0;
                foodRevenueToday =
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

                        AND COALESCE(PaidAt,CreatedAt) >= @p0
                        AND COALESCE(PaidAt,CreatedAt) < @p1
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

            // =====================================================
            // 7. MA TRẬN 40 PHÒNG VẬT LÝ (ĐỒNG BỘ VÀ CẬP NHẬT TRỰC TIẾP VÀO DATABASE)
            // =====================================================
            // The date-aware matrix loads through the shared, permission-checked API.


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

            DateTime today = DateTime.Today;

            var bookings =
                db.Bookings
                  .Include(b => b.Room)
                  .Include(b => b.ThanhToans)
                  .Where(
                      b =>
                      (b.Status == "Đã xác nhận" || b.Status == "Chờ xác nhận")
                      &&
                      DbFunctions.TruncateTime(b.CheckInDate) == today
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
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult CheckInBooking(
            int id
        )
        {
            if (!IsStaff()) return new HttpStatusCodeResult(403);
            var booking = db.Bookings.Find(id);
            if (booking == null) return HttpNotFound();
            try { HotelOperations.ValidateCheckIn(db, booking); }
            catch (HotelRuleException ex) { FailOperation(); TempData["StaffError"] = ex.Message; return RedirectToAction("TodayCheckIns"); }
            booking.Status = "Đang ở";
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["StaffSuccess"] = "Đã nhận phòng cho đơn #" + id;
            return RedirectToAction("TodayCheckIns");
        }


        // =====================================================
        // XÁC NHẬN KHÁCH TRẢ PHÒNG
        // =====================================================

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
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
                FailOperation(); TempData["StaffError"] =
                    "Chỉ có thể trả phòng đối với khách đang ở.";

                return RedirectToAction(
                    "Bookings"
                );
            }
            decimal paidAmount = db.ThanhToans.Where(t => t.BookingId == id && t.TrangThai == "Đã thanh toán").Select(t => (decimal?)t.SoTien).Sum() ?? 0;
            if (paidAmount < booking.TotalAmount)
            {
                FailOperation(); TempData["StaffError"] =
                    "Khách chưa thanh toán. Vui lòng xác nhận thanh toán trước khi trả phòng.";

                return RedirectToAction(
                    "Bookings"
                );
            }

            booking.Status =
                "Đã trả phòng";

            db.SaveChanges();

            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);

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
                        b.CustomerEmail.Contains(keyword) ||
                        (b.SpecialRequest != null && b.SpecialRequest.Contains(keyword))
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
        [QLKhachSan.Filters.HotelAntiForgery]
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
                FailOperation(); TempData["StaffError"] =
                    "Chỉ có thể xác nhận đơn đang chờ xác nhận.";

                return RedirectToAction(
                    "Bookings"
                );
            }


            try { HotelOperations.EnsureReservation(db, booking); }
            catch (HotelRuleException ex) { FailOperation(); TempData["StaffError"] = ex.Message; return RedirectToAction("Bookings"); }
            booking.Status =
                "Đã xác nhận";


            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);


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
        [QLKhachSan.Filters.HotelAntiForgery]
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
                FailOperation(); TempData["StaffError"] =
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
                FailOperation(); TempData["StaffError"] =
                    "Đơn đã thanh toán. Không thể hủy trực tiếp.";

                return RedirectToAction(
                    "Bookings"
                );
            }


            HotelOperations.ReturnPromotionUse(db, id);
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
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);


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
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult ConfirmPayment(
            int id
        )
        {
            if (!IsStaff()) return new HttpStatusCodeResult(403);
            var booking = db.Bookings.Find(id);
            if (booking == null) return HttpNotFound();
            try { HotelOperations.ConfirmBookingPayment(db, booking, Request.Headers["X-Payment-Evidence"] ?? Request["paymentEvidence"], Convert.ToInt32(Session["UserId"])); }
            catch (HotelRuleException ex) { FailOperation(); TempData["StaffError"] = ex.Message; return RedirectToAction("Bookings"); }
            TempData["StaffSuccess"] = "Đã xác minh và ghi nhận thanh toán đơn #" + id;
            return RedirectToAction("Bookings");
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
        [QLKhachSan.Filters.HotelAntiForgery]
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
            string specialRequest,
            string[] roomNumbers
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
                FailOperation(); TempData["StaffError"] =
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
                FailOperation(); TempData["StaffError"] =
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
                FailOperation(); TempData["StaffError"] =
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
                FailOperation(); TempData["StaffError"] =
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
                FailOperation(); TempData["StaffError"] =
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
                FailOperation(); TempData["StaffError"] =
                    "Số khách vượt quá sức chứa của số phòng đã chọn.";

                return RedirectToAction(
                    "CreateBooking"
                );
            }


            // =================================================
            // KIỂM TRA SỐ PHÒNG CÒN TRỐNG
            // =================================================

            System.Collections.Generic.List<PhysicalRoom> selectedRooms;
            try
            {
                selectedRooms = HotelOperations.SelectRooms(db, roomId, checkInDate.Date, checkOutDate.Date, roomCount, roomNumbers, false);
                specialRequest = HotelOperations.AssignmentNotes(selectedRooms, specialRequest);
            }
            catch (HotelRuleException ex)
            {
                FailOperation(); TempData["StaffError"] = ex.Message;
                return RedirectToAction("CreateBooking");
            }

            if (
                paymentMethod !=
                    "Tiền mặt" &&
                paymentMethod !=
                    "Chuyển khoản"
            )
            {
                FailOperation(); TempData["StaffError"] =
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
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);


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
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);


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
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult UpdateFoodOrderStatus(
            int id,
            string status
        )
        {
            try { HotelOperations.ChangeFoodStatus(db, id, (status ?? "").Trim()); }
            catch (HotelRuleException ex) { FailOperation(); TempData["StaffFoodError"] = ex.Message; return RedirectToAction("FoodOrders"); }
            TempData["StaffFoodSuccess"] = "Đã cập nhật đơn món.";
            return RedirectToAction("FoodOrders");
        }
        // =====================================================
        // NHÂN VIÊN - XÁC NHẬN THANH TOÁN ĐƠN MÓN
        // =====================================================

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult ConfirmFoodPayment(
            int id
        )
        {
            try { HotelOperations.ConfirmFoodPayment(db, id, Request.Headers["X-Payment-Evidence"] ?? Request["paymentEvidence"], Convert.ToInt32(Session["UserId"])); }
            catch (HotelRuleException ex) { FailOperation(); TempData["StaffFoodError"] = ex.Message; return RedirectToAction("FoodOrders"); }
            TempData["StaffFoodSuccess"] = "Đã xác minh và thu tiền đơn món.";
            return RedirectToAction("FoodOrders");
        }

        // =========================================================
        // NHÂN VIÊN - QUẢN LÝ YÊU CẦU HOÀN TIỀN
        // =========================================================

        [HttpGet]
        public ActionResult RefundRequests(string keyword = "", string status = "")
        {
            if (!IsStaff())
            {
                return RedirectToAction("Index", "Home", new { loginRequired = 1 });
            }

            keyword = (keyword ?? "").Trim().ToLower();
            status = (status ?? "").Trim();

            var list = new List<StaffRefundListViewModel>();
            try
            {
                var query = db.RefundRequests.Include(r => r.Booking).AsQueryable();

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    query = query.Where(r => r.BookingId.ToString().Contains(keyword)
                                          || r.CustomerName.ToLower().Contains(keyword)
                                          || r.CustomerPhone.ToLower().Contains(keyword)
                                          || r.CustomerEmail.ToLower().Contains(keyword)
                                          || r.BankName.ToLower().Contains(keyword));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(r => r.Status == status);
                }

                list = query.OrderByDescending(r => r.RequestedAt)
                                .ToList()
                                .Select(r => new StaffRefundListViewModel
                                {
                                    RefundRequestId = r.RefundRequestId,
                                    BookingId = r.BookingId,
                                    RoomName = r.Booking != null && r.Booking.Room != null ? r.Booking.Room.RoomName : "Phòng",
                                    CustomerName = r.CustomerName,
                                    CustomerPhone = r.CustomerPhone,
                                    CustomerEmail = r.CustomerEmail,
                                    CheckInDate = r.Booking != null ? r.Booking.CheckInDate : DateTime.MinValue,
                                    CheckOutDate = r.Booking != null ? r.Booking.CheckOutDate : DateTime.MinValue,
                                    PaidAmount = r.PaidAmount,
                                    RefundRate = r.RefundRate,
                                    RefundAmount = r.RefundAmount,
                                    BankName = r.BankName,
                                    AccountNumber = r.AccountNumber,
                                    AccountHolder = r.AccountHolder,
                                    Reason = r.Reason,
                                    Status = r.Status,
                                    RequestedAt = r.RequestedAt,
                                    RejectedReason = r.RejectedReason
                                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("RefundRequests table not found yet: " + ex.Message);
            }

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;

            return View(list);
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult VerifyRefundRequest(int id)
        {
            if (!IsStaff())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
            }

            int staffUserId = Convert.ToInt32(Session["UserId"]);

            try
            {
                var req = db.RefundRequests.FirstOrDefault(r => r.RefundRequestId == id);
                if (req == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu hoàn tiền." });
                }

                if (req.Status != "ChoDuyet")
                {
                    return Json(new { success = false, message = "Yêu cầu đã được xử lý trước đó." });
                }

                req.Status = "NhanVienDaDuyet";
                req.StaffUserId = staffUserId;
                req.StaffVerifiedAt = DateTime.UtcNow;

                db.SaveChanges();

                return Json(new { success = true, message = "Đã xác nhận yêu cầu hợp lệ và chuyển cho Admin xử lý chuyển khoản." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("VerifyRefundRequest error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra khi xử lý yêu cầu." });
            }
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult RejectRefundRequest(int id, string reason)
        {
            if (!IsStaff())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
            }

            string rejectReason = (reason ?? "").Trim();
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                return Json(new { success = false, message = "Vui lòng nhập lý do từ chối." });
            }

            int staffUserId = Convert.ToInt32(Session["UserId"]);

            try
            {
                var req = db.RefundRequests.FirstOrDefault(r => r.RefundRequestId == id);
                if (req == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu hoàn tiền." });
                }

                if (req.Status == "DaHoanTien")
                {
                    return Json(new { success = false, message = "Không thể từ chối yêu cầu đã được hoàn tiền." });
                }

                req.Status = "TuChoi";
                req.RejectedReason = rejectReason;
                req.StaffUserId = staffUserId;
                req.StaffVerifiedAt = DateTime.UtcNow;

                db.SaveChanges();

                return Json(new { success = true, message = "Đã từ chối yêu cầu hoàn tiền." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("RejectRefundRequest error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra khi xử lý yêu cầu." });
            }
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
