using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using QLKhachSan.Models;

namespace QLKhachSan.Controllers
{
    public class BookingHistoryController : HotelController
    {
// =========================================================
        // LỊCH SỬ ĐẶT PHÒNG CỦA TÀI KHOẢN ĐANG ĐĂNG NHẬP
        // =========================================================

        public ActionResult Index()
        {
            // =====================================================
            // CHƯA ĐĂNG NHẬP
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


            // =====================================================
            // LẤY USER ID TỪ SESSION
            // =====================================================

            int userId;

            bool validUserId =
                int.TryParse(
                    Session["UserId"].ToString(),
                    out userId
                );


            if (!validUserId || userId <= 0)
            {
                Session.Remove("UserId");

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
            // LẤY BOOKING CỦA ĐÚNG TÀI KHOẢN
            // =====================================================

            string sql = @"
                SELECT
                    b.BookingId AS BookingId,
                    b.RoomId AS RoomId,
                    r.RoomName AS RoomName,
                    r.RoomType AS RoomType,
                    r.ImageUrl AS ImageUrl,
                    b.CheckInDate AS CheckInDate,
                    b.CheckOutDate AS CheckOutDate,
                    b.GuestCount AS GuestCount,
                    b.RoomCount AS RoomCount,
                    b.TotalAmount AS TotalAmount,
                    b.PaymentMethod AS PaymentMethod,
                    b.Status AS Status,
                    b.CreatedAt AS CreatedAt

                FROM dbo.Bookings AS b

                INNER JOIN dbo.Rooms AS r
                    ON b.RoomId = r.RoomId

                WHERE b.UserId = @p0

                ORDER BY b.CreatedAt DESC
            ";


            List<BookingHistoryViewModel> bookings =
                db.Database
                  .SqlQuery<BookingHistoryViewModel>(
                      sql,
                      userId
                  )
                  .ToList();

            DateTime now = DateTime.UtcNow;
            List<RefundRequest> existingRefunds = new List<RefundRequest>();
            try
            {
                existingRefunds = db.RefundRequests.Where(r => r.UserId == userId).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("RefundRequests table not found yet: " + ex.Message);
            }

            foreach (var b in bookings)
            {
                var refReq = existingRefunds.FirstOrDefault(r => r.BookingId == b.BookingId);
                decimal rate = CalculateRefundRate(b.CheckInDate, now);
                b.RefundRate = rate;
                b.EstimatedRefundAmount = Math.Round(b.TotalAmount * (rate / 100m), 0);

                if (refReq != null)
                {
                    b.CanRequestRefund = false;
                    b.RefundStatus = refReq.Status;
                    b.RefundRate = refReq.RefundRate;
                    b.EstimatedRefundAmount = refReq.RefundAmount;
                    b.RefundRejectedReason = refReq.RejectedReason;

                    switch (refReq.Status)
                    {
                        case "ChoDuyet":
                            b.RefundStatusText = "Đang chờ duyệt hoàn tiền";
                            break;
                        case "NhanVienDaDuyet":
                            b.RefundStatusText = "Nhân viên đã xác nhận - Chờ Admin chuyển khoản";
                            break;
                        case "DaHoanTien":
                            b.RefundStatusText = "Đã hoàn tiền thành công (" + refReq.RefundAmount.ToString("N0") + " VNĐ)";
                            break;
                        case "TuChoi":
                            b.RefundStatusText = "Yêu cầu hoàn tiền bị từ chối";
                            break;
                        default:
                            b.RefundStatusText = refReq.Status;
                            break;
                    }
                }
                else
                {
                    bool isCancelled = (b.Status ?? "").Equals("Hủy", System.StringComparison.OrdinalIgnoreCase)
                                     || (b.Status ?? "").Equals("Đã hủy", System.StringComparison.OrdinalIgnoreCase)
                                     || (b.Status ?? "").Equals("Cancelled", System.StringComparison.OrdinalIgnoreCase);

                    b.CanRequestRefund = !isCancelled && rate > 0;
                    b.RefundStatus = "";
                    b.RefundStatusText = "";
                }
            }

            return View(bookings);
        }

        // =========================================================
        // YÊU CẦU HOÀN TIỀN CỦA KHÁCH HÀNG
        // =========================================================

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult RequestRefund(CreateRefundRequestInputModel input)
        {
            if (Session["UserId"] == null)
            {
                TempData["RefundError"] = "Vui lòng đăng nhập để thực hiện.";
                return RedirectToAction("Index");
            }

            int userId;
            if (!int.TryParse(Session["UserId"].ToString(), out userId) || userId <= 0)
            {
                TempData["RefundError"] = "Phiên đăng nhập không hợp lệ.";
                return RedirectToAction("Index");
            }

            if (input == null || input.BookingId <= 0)
            {
                TempData["RefundError"] = "Thông tin đơn đặt phòng không hợp lệ.";
                return RedirectToAction("Index");
            }

            string bankName = (input.BankName ?? "").Trim();
            string accountNumber = (input.AccountNumber ?? "").Trim();
            string accountHolder = (input.AccountHolder ?? "").Trim();
            string reason = (input.Reason ?? "").Trim();

            if (string.IsNullOrWhiteSpace(bankName) || string.IsNullOrWhiteSpace(accountNumber) || string.IsNullOrWhiteSpace(accountHolder) || string.IsNullOrWhiteSpace(reason))
            {
                TempData["RefundError"] = "Vui lòng nhập đầy đủ Tên ngân hàng, Số tài khoản, Tên chủ tài khoản và Lý do hoàn tiền.";
                return RedirectToAction("Index");
            }

            try
            {
                var booking = db.Bookings.FirstOrDefault(b => b.BookingId == input.BookingId && b.UserId == userId);
                if (booking == null)
                {
                    TempData["RefundError"] = "Không tìm thấy đơn đặt phòng hoặc bạn không có quyền truy cập.";
                    return RedirectToAction("Index");
                }

                bool alreadyRequested = db.RefundRequests.Any(r => r.BookingId == booking.BookingId);
                if (alreadyRequested)
                {
                    TempData["RefundError"] = "Đơn đặt phòng này đã gửi yêu cầu hoàn tiền trước đó.";
                    return RedirectToAction("Index");
                }

                DateTime now = DateTime.UtcNow;
                decimal rate = CalculateRefundRate(booking.CheckInDate, now);

                if (rate <= 0)
                {
                    TempData["RefundError"] = "Đơn đặt phòng này đã quá hạn thời gian được hoàn tiền theo chính sách.";
                    return RedirectToAction("Index");
                }

                decimal refundAmount = Math.Round(booking.TotalAmount * (rate / 100m), 0);

                var req = new RefundRequest
                {
                    BookingId = booking.BookingId,
                    UserId = userId,
                    CustomerName = !string.IsNullOrWhiteSpace(booking.CustomerName) ? booking.CustomerName : (Session["FullName"]?.ToString() ?? "Khách hàng"),
                    CustomerPhone = !string.IsNullOrWhiteSpace(booking.CustomerPhone) ? booking.CustomerPhone : "",
                    CustomerEmail = !string.IsNullOrWhiteSpace(booking.CustomerEmail) ? booking.CustomerEmail : (Session["Email"]?.ToString() ?? ""),
                    PaidAmount = booking.TotalAmount,
                    RefundRate = rate,
                    RefundAmount = refundAmount,
                    BankName = bankName,
                    AccountNumber = accountNumber,
                    AccountHolder = accountHolder.ToUpper(),
                    Reason = reason,
                    Status = "ChoDuyet",
                    RequestedAt = now
                };

                db.RefundRequests.Add(req);
                db.SaveChanges();

                TempData["RefundSuccess"] = "Đã gửi yêu cầu hoàn tiền thành công (" + rate + "% = " + refundAmount.ToString("N0") + " VNĐ). Yêu cầu của bạn đang được nhân viên và quản lý xem xét.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("RequestRefund Error: " + ex.Message);
                TempData["RefundError"] = "Có lỗi xảy ra khi gửi yêu cầu hoàn tiền. Vui lòng thử lại.";
            }

            return RedirectToAction("Index");
        }

        public static decimal CalculateRefundRate(DateTime checkInDate, DateTime requestTime)
        {
            DateTime checkInTime = checkInDate.Date.AddHours(14);
            TimeSpan timeDiff = checkInTime - requestTime;
            double hoursRemaining = timeDiff.TotalHours;

            if (hoursRemaining >= 72)
            {
                return 100m;
            }
            if (hoursRemaining >= 48)
            {
                return 80m;
            }
            if (hoursRemaining >= 24)
            {
                return 50m;
            }
            if (hoursRemaining > 0)
            {
                return 20m;
            }
            return 0m;
        }

        // =========================================================
        // CHI TIẾT MỘT BOOKING
        // =========================================================

        public ActionResult Details(int id)
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


            // =====================================================
            // 2. ĐỌC USER ID TỪ SESSION
            // =====================================================

            int userId;


            bool validUserId =
                int.TryParse(
                    Session["UserId"].ToString(),
                    out userId
                );


            if (!validUserId || userId <= 0)
            {
                Session.Remove("UserId");


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
            // 3. KIỂM TRA BOOKING CÓ THUỘC USER NÀY KHÔNG
            //
            // KHÔNG cho người dùng đổi URL để xem booking người khác
            // =====================================================

            int ownedBookingCount =
                db.Database
                  .SqlQuery<int>(
                      @"
                SELECT COUNT(1)

                FROM dbo.Bookings

                WHERE
                    BookingId = @p0
                    AND UserId = @p1
              ",
                      id,
                      userId
                  )
                  .FirstOrDefault();


            if (ownedBookingCount <= 0)
            {
                return HttpNotFound();
            }


            // =====================================================
            // 4. LẤY BOOKING
            // =====================================================

            var booking =
                db.Bookings
                  .FirstOrDefault(
                      b =>
                      b.BookingId == id
                  );


            if (booking == null)
            {
                return HttpNotFound();
            }


            // =====================================================
            // 5. LẤY PHÒNG
            // =====================================================

            var room =
                db.Rooms
                  .FirstOrDefault(
                      r =>
                      r.RoomId ==
                      booking.RoomId
                  );


            if (room == null)
            {
                return HttpNotFound();
            }


            // =====================================================
            // 6. LẤY KHUYẾN MÃI + TIỀN GIẢM
            //
            // Booking.cs EDMX có thể chưa có MaKhuyenMai/TienGiam,
            // nên đọc trực tiếp từ database.
            // =====================================================

            BookingExtraData extra =
                db.Database
                  .SqlQuery<BookingExtraData>(
                      @"
                SELECT
                    MaKhuyenMai,

                    ISNULL(
                        TienGiam,
                        0
                    ) AS TienGiam

                FROM dbo.Bookings

                WHERE BookingId = @p0
              ",
                      id
                  )
                  .FirstOrDefault();


            decimal discountAmount =
                extra != null
                    ? extra.TienGiam
                    : 0m;


            // =====================================================
            // 7. LẤY DANH SÁCH DỊCH VỤ
            // =====================================================

            List<BookingServiceViewModel> services =
                (
                    from bookingService
                    in db.DatPhongDichVus

                    join service
                    in db.DichVus

                    on bookingService.MaDichVu
                    equals service.MaDichVu

                    where
                        bookingService.BookingId ==
                        id

                    select
                        new BookingServiceViewModel
                        {
                            ServiceId =
                                service.MaDichVu,

                            ServiceName =
                                service.TenDichVu,

                            Unit =
                                service.DonVi,

                            Quantity =
                                bookingService.SoLuong,

                            UnitPrice =
                                bookingService.DonGia,

                            TotalPrice =
                                bookingService.ThanhTien
                        }
                )
                .ToList();


            decimal serviceAmount =
                services.Sum(
                    s =>
                    s.TotalPrice
                );


            // =====================================================
            // 8. LẤY THÔNG TIN KHUYẾN MÃI
            // =====================================================

            KhuyenMai promotion = null;


            if (
                extra != null &&
                extra.MaKhuyenMai.HasValue
            )
            {
                int promotionId =
                    extra.MaKhuyenMai.Value;


                promotion =
                    db.KhuyenMais
                      .FirstOrDefault(
                          k =>
                          k.MaKhuyenMai ==
                          promotionId
                      );
            }


            // =====================================================
            // 9. LẤY THANH TOÁN
            // =====================================================

            var payment =
                db.ThanhToans

                  .Where(
                      t =>
                      t.BookingId ==
                      id
                  )

                  .OrderByDescending(
                      t =>
                      t.NgayThanhToan
                  )

                  .FirstOrDefault();


            // =====================================================
            // 10. TÍNH LẠI TIỀN PHÒNG TỪ DỮ LIỆU ĐÃ LƯU
            //
            // Total =
            // Room + Service - Discount
            //
            // => Room =
            // Total - Service + Discount
            // =====================================================

            decimal roomAmount =
                booking.TotalAmount
                -
                serviceAmount
                +
                discountAmount;


            if (roomAmount < 0)
            {
                roomAmount = 0;
            }


            // =====================================================
            // 11. TẠO VIEW MODEL
            // =====================================================

            BookingDetailViewModel model =
                new BookingDetailViewModel
                {
                    BookingId =
                        booking.BookingId,

                    RoomId =
                        room.RoomId,

                    RoomName =
                        room.RoomName,

                    RoomType =
                        room.RoomType,

                    ImageUrl =
                        room.ImageUrl,


                    CustomerName =
                        booking.CustomerName,

                    CustomerPhone =
                        booking.CustomerPhone,

                    CustomerEmail =
                        booking.CustomerEmail,

                    CustomerCCCD =
                        booking.CustomerCCCD,


                    CheckInDate =
                        booking.CheckInDate,

                    CheckOutDate =
                        booking.CheckOutDate,

                    GuestCount =
                        booking.GuestCount,

                    RoomCount =
                        booking.RoomCount,

                    SpecialRequest =
                        booking.SpecialRequest,


                    PaymentMethod =
                        booking.PaymentMethod,

                    TotalAmount =
                        booking.TotalAmount,

                    Status =
                        booking.Status,

                    CreatedAt =
                        booking.CreatedAt,


                    PromotionCode =
                        promotion != null
                            ? promotion.MaCode
                            : null,

                    PromotionName =
                        promotion != null
                            ? promotion.TenKhuyenMai
                            : null,

                    DiscountAmount =
                        discountAmount,


                    RoomAmount =
                        roomAmount,

                    ServiceAmount =
                        serviceAmount,


                    Services =
                        services,


                    PaymentStatus = HotelOperations.PaymentStatus(booking),

                    PaymentAmount = HotelOperations.PaidAmount(booking),

                    PaymentDate =
                        payment != null
                            ? (System.DateTime?)
                              payment.NgayThanhToan
                            : null,

                    TransactionCode =
                        payment != null
                            ? payment.MaGiaoDich
                            : null,

                    PaymentNote =
                        payment != null
                            ? payment.GhiChu
                            : null
                };


            // =====================================================
            // 12. TRẢ SANG DETAILS.CSHTML
            // =====================================================

            return View(model);
        }
        // =========================================================
        // HỦY ĐẶT PHÒNG
        // =========================================================

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult Cancel(int id)
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


            // =====================================================
            // 2. ĐỌC USER ID
            // =====================================================

            int userId;


            bool validUserId =
                int.TryParse(
                    Session["UserId"].ToString(),
                    out userId
                );


            if (!validUserId || userId <= 0)
            {
                Session.Clear();

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
            // 3. KIỂM TRA BOOKING CÓ THUỘC USER NÀY KHÔNG
            // =====================================================

            int ownedBookingCount =
                db.Database
                  .SqlQuery<int>(
                      @"
                SELECT COUNT(1)

                FROM dbo.Bookings

                WHERE
                    BookingId = @p0
                    AND UserId = @p1
              ",
                      id,
                      userId
                  )
                  .FirstOrDefault();


            if (ownedBookingCount <= 0)
            {
                return HttpNotFound();
            }


            // =====================================================
            // 4. LẤY BOOKING
            // =====================================================

            var booking =
                db.Bookings
                  .FirstOrDefault(
                      b =>
                      b.BookingId == id
                  );


            if (booking == null)
            {
                return HttpNotFound();
            }


            // =====================================================
            // 5. KIỂM TRA TRẠNG THÁI
            // =====================================================

            if (
                booking.Status != "Chờ xác nhận" &&
                booking.Status != "Đã xác nhận"
            )
            {
                FailOperation(); TempData["BookingError"] =
                    "Booking này không thể hủy ở trạng thái hiện tại.";


                return RedirectToAction(
                    "Details",
                    new
                    {
                        id = id
                    }
                );
            }


            // =====================================================
            // 6. KHÔNG CHO HỦY KHI ĐÃ TỚI NGÀY NHẬN PHÒNG
            // =====================================================

            if (
                booking.CheckInDate.Date <=
                System.DateTime.Today
            )
            {
                FailOperation(); TempData["BookingError"] =
                    "Không thể hủy booking khi đã tới ngày nhận phòng.";


                return RedirectToAction(
                    "Details",
                    new
                    {
                        id = id
                    }
                );
            }


            // =====================================================
            // 7. KIỂM TRA THANH TOÁN
            // =====================================================

            bool hasPaidPayment =
                db.ThanhToans.Any(
                    t =>
                    t.BookingId == id &&
                    t.TrangThai == "Đã thanh toán"
                );


            if (hasPaidPayment)
            {
                FailOperation(); TempData["BookingError"] =
                    "Booking đã thanh toán. Vui lòng liên hệ khách sạn để được hỗ trợ hủy và hoàn tiền.";


                return RedirectToAction(
                    "Details",
                    new
                    {
                        id = id
                    }
                );
            }


            // =====================================================
            // 8. ĐỌC MÃ KHUYẾN MÃI
            // =====================================================

            BookingExtraData extra =
                db.Database
                  .SqlQuery<BookingExtraData>(
                      @"
                SELECT
                    MaKhuyenMai,

                    ISNULL(
                        TienGiam,
                        0
                    ) AS TienGiam

                FROM dbo.Bookings

                WHERE BookingId = @p0
              ",
                      id
                  )
                  .FirstOrDefault();


            // =====================================================
            // 9. DÙNG TRANSACTION ĐỂ HỦY AN TOÀN
            // =====================================================

            using (
                var transaction =
                    db.Database.BeginTransaction()
            )
            {
                try
                {
                    HotelOperations.LockInventory(db);
                    db.Entry(booking).Reload();
                    if ((booking.Status != "Chờ xác nhận" && booking.Status != "Đã xác nhận") || db.ThanhToans.Any(p => p.BookingId == id && p.TrangThai == "Đã thanh toán"))
                        throw new System.InvalidOperationException("Đơn đã thay đổi. Vui lòng tải lại.");
                    // =============================================
                    // ĐỔI TRẠNG THÁI BOOKING
                    // =============================================

                    booking.Status =
                        "Đã hủy";


                    // =============================================
                    // ĐỔI TRẠNG THÁI THANH TOÁN CHƯA HOÀN TẤT
                    // =============================================

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


                    // =============================================
                    // HOÀN LẠI 1 LƯỢT MÃ KHUYẾN MÃI
                    // =============================================

                    if (
                        extra != null &&
                        extra.MaKhuyenMai.HasValue
                    )
                    {
                        int promotionId =
                            extra.MaKhuyenMai.Value;


                        var promotion =
                            db.KhuyenMais
                              .FirstOrDefault(
                                  k =>
                                  k.MaKhuyenMai ==
                                  promotionId
                              );


                        if (
                            promotion != null &&
                            promotion.SoLuotDaSuDung > 0
                        )
                        {
                            promotion.SoLuotDaSuDung -= 1;
                        }
                    }


                    // =============================================
                    // LƯU
                    // =============================================

                    db.SaveChanges();
                    PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);


                    transaction.Commit();


                    TempData["BookingSuccess"] =
                        "Hủy đặt phòng thành công.";
                }
                catch
                {
                    transaction.Rollback();


                    FailOperation(); TempData["BookingError"] =
                        "Có lỗi xảy ra khi hủy đặt phòng. Vui lòng thử lại.";
                }
            }


            return RedirectToAction(
                "Details",
                new
                {
                    id = id
                }
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

            base.Dispose(disposing);
        }
    }
}