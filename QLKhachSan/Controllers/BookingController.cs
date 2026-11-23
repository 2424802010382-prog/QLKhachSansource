using QLKhachSan.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;

namespace QLKhachSan.Controllers
{
    public class BookingController : HotelController
    {
// =========================================================
        // LẤY DANH SÁCH PHÒNG TRỐNG THEO THỜI GIAN (CHỐNG TRÙNG LỊCH)
        // =========================================================

        [HttpGet]
        public JsonResult GetAvailableRooms(DateTime checkIn, DateTime checkOut, int? capacity)
        {
            if (!ModelState.IsValid || checkIn.Date < DateTime.Today || checkOut.Date <= checkIn.Date)
                return Json(new { success = false, message = "Khoảng ngày không hợp lệ." }, JsonRequestBehavior.AllowGet);
            try
            {
                var rooms = db.Rooms.AsNoTracking().Where(r => r.IsActive).ToList()
                    .Select(r => new { Room = r, Available = HotelOperations.AvailableRooms(db, r.RoomId, checkIn.Date, checkOut.Date).Count })
                    .Where(x => x.Available > 0 && (!capacity.HasValue || x.Room.Capacity >= capacity.Value))
                    .Select(x => new { roomId = x.Room.RoomId, roomName = x.Room.RoomName, roomType = x.Room.RoomType,
                        price = x.Room.Price, capacity = x.Room.Capacity, description = x.Room.Description,
                        imageUrl = x.Room.ImageUrl, availableRooms = x.Available }).ToList();
                return Json(new { success = true, rooms }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(new { success = false, message = "Không tải được phòng trống. Vui lòng thử lại." }, JsonRequestBehavior.AllowGet);
            }
        }


        // =========================================================
        // LẤY DANH SÁCH DỊCH VỤ
        // =========================================================

        [HttpGet]
        public JsonResult GetServices()
        {
            try
            {
                var services = db.DichVus
                    .Where(d => d.HoatDong)
                    .OrderBy(d => d.TenDichVu)
                    .Select(d => new
                    {
                        id = d.MaDichVu,
                        name = d.TenDichVu,
                        description = d.MoTa,
                        price = d.Gia,
                        unit = d.DonVi
                    })
                    .ToList();


                return Json(
                    services,
                    JsonRequestBehavior.AllowGet
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(
                    new
                    {
                        success = false,
                        message =
                            "Không tải được dịch vụ. Vui lòng thử lại sau."
                    },
                    JsonRequestBehavior.AllowGet
                );
            }
        }


        // =========================================================
        // KIỂM TRA MÃ KHUYẾN MÃI
        // =========================================================

        [HttpGet]
        public JsonResult GetAvailablePromotions()
        {
            var today = DateTime.Today;
            var promotions = db.KhuyenMais.AsNoTracking()
                .Where(k => k.HoatDong && k.NgayBatDau <= today && k.NgayKetThuc >= today
                    && (!k.SoLuotSuDungToiDa.HasValue || k.SoLuotDaSuDung < k.SoLuotSuDungToiDa.Value))
                .OrderBy(k => k.DonToiThieu).ThenBy(k => k.MaCode)
                .Select(k => new {
                    code = k.MaCode, name = k.TenKhuyenMai, type = k.LoaiGiamGia,
                    value = k.GiaTriGiam, minAmount = k.DonToiThieu, maxDiscount = k.GiamToiDa
                }).ToList();
            return Json(promotions, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult CheckPromotion(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Json(
                    new
                    {
                        success = false,
                        message =
                            "Vui lòng nhập mã khuyến mãi."
                    },
                    JsonRequestBehavior.AllowGet
                );
            }


            string ma =
                code.Trim().ToUpper();


            DateTime today =
                DateTime.Today;


            var promotion =
                db.KhuyenMais.FirstOrDefault(k =>
                    k.MaCode == ma &&
                    k.HoatDong &&
                    k.NgayBatDau <= today &&
                    k.NgayKetThuc >= today
                );


            if (promotion == null)
            {
                return Json(
                    new
                    {
                        success = false,
                        message =
                            "Mã khuyến mãi không tồn tại hoặc đã hết hạn."
                    },
                    JsonRequestBehavior.AllowGet
                );
            }


            if (
                promotion.SoLuotSuDungToiDa.HasValue &&
                promotion.SoLuotDaSuDung >=
                promotion.SoLuotSuDungToiDa.Value
            )
            {
                return Json(
                    new
                    {
                        success = false,
                        message =
                            "Mã khuyến mãi đã hết lượt sử dụng."
                    },
                    JsonRequestBehavior.AllowGet
                );
            }


            // Quan trọng:
            // các tên field bên dưới phải khớp Index.cshtml

            return Json(
                new
                {
                    success = true,

                    id =
                        promotion.MaKhuyenMai,

                    code =
                        promotion.MaCode,

                    name =
                        promotion.TenKhuyenMai,

                    type =
                        promotion.LoaiGiamGia,

                    value =
                        promotion.GiaTriGiam,

                    minAmount =
                        promotion.DonToiThieu,

                    maxDiscount =
                        promotion.GiamToiDa,

                    message =
                        "Áp dụng mã thành công: "
                        + promotion.TenKhuyenMai
                },
                JsonRequestBehavior.AllowGet
            );
        }


        // =========================================================
        // LẤY DANH SÁCH THÔNG TIN KHÁCH HÀNG ĐÃ LƯU / ĐÃ TỪNG ĐẶT PHÒNG
        // =========================================================

        [HttpGet]
        public JsonResult GetSavedCustomerProfiles()
        {
            if (Session["UserId"] == null)
            {
                return Json(new { success = false, profiles = new object[0] }, JsonRequestBehavior.AllowGet);
            }

            if (!int.TryParse(Convert.ToString(Session["UserId"]), out int userId))
            {
                return Json(new { success = false, profiles = new object[0] }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var user = db.Users.Find(userId);
                var pastBookings = db.Bookings
                    .AsNoTracking()
                    .Where(b => b.UserId == userId && !string.IsNullOrEmpty(b.CustomerName))
                    .OrderByDescending(b => b.CreatedAt)
                    .ToList();

                var profilesList = new List<object>();
                var addedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Thông tin từ tài khoản chính
                if (user != null)
                {
                    var userLastBooking = pastBookings.FirstOrDefault(b =>
                        (b.CustomerName ?? "").Trim().Equals((user.FullName ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

                    string userPhone = userLastBooking?.CustomerPhone ?? "";
                    string userEmail = string.IsNullOrEmpty(user.Email) ? (userLastBooking?.CustomerEmail ?? "") : user.Email;
                    string userCccd = userLastBooking?.CustomerCCCD ?? "";

                    string key = $"{user.FullName?.Trim()}|{userPhone.Trim()}|{userCccd.Trim()}";
                    addedKeys.Add(key);

                    string detailStr = !string.IsNullOrEmpty(userPhone) ? userPhone : userEmail;
                    if (!string.IsNullOrEmpty(userCccd)) detailStr += $" - CCCD: {userCccd}";

                    profilesList.Add(new
                    {
                        name = user.FullName ?? "",
                        phone = userPhone,
                        email = userEmail,
                        cccd = userCccd,
                        label = $"👤 Thông tin của bạn ({user.FullName})" + (!string.IsNullOrEmpty(detailStr) ? $" - {detailStr}" : "")
                    });
                }

                // 2. Thông tin khách hàng đã từng được đặt từ tài khoản này
                foreach (var booking in pastBookings)
                {
                    string name = (booking.CustomerName ?? "").Trim();
                    string phone = (booking.CustomerPhone ?? "").Trim();
                    string email = (booking.CustomerEmail ?? "").Trim();
                    string cccd = (booking.CustomerCCCD ?? "").Trim();

                    if (string.IsNullOrEmpty(name)) continue;

                    string key = $"{name}|{phone}|{cccd}";
                    if (!addedKeys.Contains(key))
                    {
                        addedKeys.Add(key);

                        string infoStr = !string.IsNullOrEmpty(phone) ? phone : email;
                        if (!string.IsNullOrEmpty(cccd)) infoStr += $" - CCCD: {cccd}";

                        profilesList.Add(new
                        {
                            name = name,
                            phone = phone,
                            email = email,
                            cccd = cccd,
                            label = $"👥 Khách đã đặt trước: {name}" + (!string.IsNullOrEmpty(infoStr) ? $" ({infoStr})" : "")
                        });
                    }
                }

                return Json(new { success = true, profiles = profilesList }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(new { success = false, profiles = new object[0] }, JsonRequestBehavior.AllowGet);
            }
        }


        // =========================================================
        // TẠO ĐẶT PHÒNG
        // =========================================================

        [HttpPost]
        public JsonResult Create(
            TaoDatPhongRequest request)
        {
            using (
                var transaction =
                    db.Database.BeginTransaction()
            )
            {
                try
                {
                    HotelOperations.LockInventory(db);
                    if (Session["UserId"] == null)
                    {
                        return Json(new
                        {
                            success = false,
                            requireLogin = true,
                            message = "Vui lòng đăng nhập tài khoản để thực hiện đặt phòng."
                        });
                    }
                    string role = Session["Role"] != null ? Session["Role"].ToString() : "";
                    if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase) || role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Tài khoản quản trị hoặc nhân viên không được phép đặt phòng với tư cách khách hàng."
                        });
                    }

                    // =============================================
                    // KIỂM TRA REQUEST
                    // =============================================

                    if (request == null || !ModelState.IsValid)
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Dữ liệu đặt phòng không hợp lệ."
                        });
                    }


                    // =============================================
                    // TÌM PHÒNG
                    // =============================================

                    var room =
                        db.Rooms.Find(
                            request.RoomId
                        );


                    if (
                        room == null ||
                        !room.IsActive
                    )
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Phòng không tồn tại hoặc đã ngừng hoạt động."
                        });
                    }


                    // =============================================
                    // KIỂM TRA THÔNG TIN KHÁCH
                    // =============================================

                    if (
                        string.IsNullOrWhiteSpace(
                            request.CustomerName
                        ) ||

                        string.IsNullOrWhiteSpace(
                            request.CustomerPhone
                        ) ||

                        string.IsNullOrWhiteSpace(
                            request.CustomerEmail
                        )
                    )
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Vui lòng nhập đầy đủ họ tên, số điện thoại và email."
                        });
                    }
                    // =============================================
                    // KIỂM TRA ĐỊNH DẠNG SỐ ĐIỆN THOẠI
                    // =============================================

                    string customerPhone =
                        request.CustomerPhone.Trim();


                    if (
                        !System.Text.RegularExpressions.Regex.IsMatch(
                            customerPhone,
                            @"^\d{10,11}$"
                        )
                    )
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                "Số điện thoại phải gồm từ 10 đến 11 chữ số."
                        });
                    }
                    // =============================================
                    // KIỂM TRA ĐỊNH DẠNG EMAIL
                    // =============================================

                    string customerEmail =
                        request.CustomerEmail.Trim();


                    if (
                        !System.Text.RegularExpressions.Regex.IsMatch(
                            customerEmail,
                            @"^[^@\s]+@[^@\s]+\.[^@\s]+$"
                        )
                    )
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                "Email không đúng định dạng."
                        });
                    }
                    // =============================================
                    // KIỂM TRA CCCD
                    // CCCD KHÔNG BẮT BUỘC
                    // NHƯNG NẾU NHẬP THÌ PHẢI ĐỦ 12 SỐ
                    // =============================================

                    if (
                        !string.IsNullOrWhiteSpace(
                            request.CustomerCCCD
                        )
                    )
                    {
                        string customerCCCD =
                            request.CustomerCCCD.Trim();


                        if (
                            !System.Text.RegularExpressions.Regex.IsMatch(
                                customerCCCD,
                                @"^\d{12}$"
                            )
                        )
                        {
                            return Json(new
                            {
                                success = false,

                                message =
                                    "CCCD phải gồm đúng 12 chữ số."
                            });
                        }
                    }

                    // =============================================
                    // KIỂM TRA PHƯƠNG THỨC THANH TOÁN
                    // =============================================

                    string paymentMethod =
                        string.IsNullOrWhiteSpace(
                            request.PaymentMethod
                        )
                            ? ""
                            : request.PaymentMethod.Trim();


                    if (
                        paymentMethod !=
                            "Thanh toán tại khách sạn" &&

                        paymentMethod !=
                            "Chuyển khoản"
                    )
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                "Phương thức thanh toán không hợp lệ."
                        });
                    }
                    // =============================================
                    // KIỂM TRA NGÀY
                    // =============================================

                    if (
                        request.CheckInDate.Date <
                        DateTime.Today
                    )
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Ngày nhận phòng không được nhỏ hơn hôm nay."
                        });
                    }


                    if (
                        request.CheckOutDate.Date <=
                        request.CheckInDate.Date
                    )
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Ngày trả phòng phải sau ngày nhận phòng."
                        });
                    }


                    // =============================================
                    // KIỂM TRA SỐ PHÒNG
                    // =============================================

                    if (request.RoomCount < 1)
                    {
                        return Json(new
                        {
                            success = false,
                            message =
                                "Số phòng không hợp lệ."
                        });
                    }


                    request.CheckInDate = request.CheckInDate.Date;
                    request.CheckOutDate = request.CheckOutDate.Date;
                    var numbers = request.RoomNumbers ?? new List<string>();
                    if (numbers.Count == 0 && !string.IsNullOrWhiteSpace(request.RoomNumber)) numbers.Add(request.RoomNumber.Trim());
                    var assigned = HotelOperations.SelectRooms(db, request.RoomId, request.CheckInDate,
                        request.CheckOutDate, request.RoomCount, numbers, true);
                    request.SpecialRequest = HotelOperations.AssignmentNotes(assigned, request.SpecialRequest);

                    // =============================================
                    // KIỂM TRA SỐ KHÁCH
                    // =============================================

                    if (
                        request.GuestCount < 1 ||

                        request.GuestCount >
                        room.Capacity *
                        request.RoomCount
                    )
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                "Số khách vượt quá sức chứa của số phòng đã chọn."
                        });
                    }


                    // =============================================
                    // TÍNH TIỀN PHÒNG
                    // =============================================

                    int nights =
                        (
                            request.CheckOutDate.Date -
                            request.CheckInDate.Date
                        ).Days;


                    decimal roomAmount =
                        room.Price *
                        nights *
                        request.RoomCount;


                    // =============================================
                    // XỬ LÝ DỊCH VỤ
                    // =============================================

                    var serviceRequests =
                        (
                            request.Services ??
                            new List<DichVuDatRequest>()
                        )

                        .Where(s =>
                            s != null &&
                            s.MaDichVu > 0 &&
                            s.SoLuong > 0
                        )

                        .GroupBy(
                            s => s.MaDichVu
                        )

                        .Select(g =>
                            new DichVuDatRequest
                            {
                                MaDichVu =
                                    g.Key,

                                SoLuong =
                                    g.Sum(
                                        x => x.SoLuong
                                    )
                            }
                        )

                        .ToList();


                    // Không cho nhập số lượng quá lớn
                    if (
                        serviceRequests.Any(
                            s => s.SoLuong > 20
                        )
                    )
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                "Số lượng dịch vụ không hợp lệ."
                        });
                    }


                    var serviceIds =
                        serviceRequests

                        .Select(
                            s => s.MaDichVu
                        )

                        .ToList();


                    var services =
                        serviceIds.Count == 0

                        ? new List<DichVu>()

                        : db.DichVus

                            .Where(d =>
                                serviceIds.Contains(
                                    d.MaDichVu
                                ) &&
                                d.HoatDong
                            )

                            .ToList();


                    if (
                        services.Count !=
                        serviceIds
                            .Distinct()
                            .Count()
                    )
                    {
                        return Json(new
                        {
                            success = false,

                            message =
                                "Có dịch vụ không tồn tại hoặc đã ngừng hoạt động."
                        });
                    }


                    decimal serviceAmount = 0;


                    foreach (
                        var serviceRequest
                        in serviceRequests
                    )
                    {
                        var service =
                            services.First(
                                d =>
                                d.MaDichVu ==
                                serviceRequest.MaDichVu
                            );


                        serviceAmount +=
                            service.Gia *
                            serviceRequest.SoLuong;
                    }


                    // =============================================
                    // XỬ LÝ KHUYẾN MÃI
                    // =============================================

                    KhuyenMai promotion = null;

                    decimal discountAmount = 0;


                    if (
                        !string.IsNullOrWhiteSpace(
                            request.PromotionCode
                        )
                    )
                    {
                        string promotionCode =
                            request
                                .PromotionCode
                                .Trim()
                                .ToUpper();


                        DateTime today =
                            DateTime.Today;


                        promotion =
                            db.KhuyenMais
                            .FirstOrDefault(k =>

                                k.MaCode ==
                                promotionCode &&

                                k.HoatDong &&

                                k.NgayBatDau <=
                                today &&

                                k.NgayKetThuc >=
                                today
                            );


                        if (promotion == null)
                        {
                            return Json(new
                            {
                                success = false,

                                message =
                                    "Mã khuyến mãi không tồn tại hoặc đã hết hạn."
                            });
                        }


                        // Khuyến mãi đang tính trên TIỀN PHÒNG
                        // để khớp với Index.cshtml

                        if (
                            roomAmount <
                            promotion.DonToiThieu
                        )
                        {
                            return Json(new
                            {
                                success = false,

                                message =
                                    "Tiền phòng chưa đạt mức tối thiểu "
                                    +
                                    promotion
                                        .DonToiThieu
                                        .ToString("N0")
                                    +
                                    "đ để sử dụng mã này."
                            });
                        }


                        if (
                            promotion
                                .SoLuotSuDungToiDa
                                .HasValue &&

                            promotion
                                .SoLuotDaSuDung >=

                            promotion
                                .SoLuotSuDungToiDa
                                .Value
                        )
                        {
                            return Json(new
                            {
                                success = false,

                                message =
                                    "Mã khuyến mãi đã hết lượt sử dụng."
                            });
                        }


                        if (
                            promotion.LoaiGiamGia ==
                            "PhanTram"
                        )
                        {
                            discountAmount =
                                roomAmount *
                                promotion.GiaTriGiam /
                                100m;


                            if (
                                promotion
                                    .GiamToiDa
                                    .HasValue
                            )
                            {
                                discountAmount =
                                    Math.Min(
                                        discountAmount,

                                        promotion
                                            .GiamToiDa
                                            .Value
                                    );
                            }
                        }

                        else if (
                            promotion.LoaiGiamGia ==
                            "SoTien"
                        )
                        {
                            discountAmount =
                                promotion.GiaTriGiam;
                        }


                        // Không cho tiền giảm lớn hơn tiền phòng
                        discountAmount =
                            Math.Min(
                                discountAmount,
                                roomAmount
                            );
                    }


                    // =============================================
                    // TỔNG THANH TOÁN
                    // =============================================

                    decimal totalAmount =
                        roomAmount +
                        serviceAmount -
                        discountAmount;


                    if (totalAmount < 0)
                    {
                        totalAmount = 0;
                    }


                    // =============================================
                    // TẠO BOOKING
                    // =============================================

                    var booking =
                        new Booking
                        {
                            RoomId =
                                request.RoomId,

                            CustomerName =
                                request
                                    .CustomerName
                                    .Trim(),

                            CustomerPhone =
                                request
                                    .CustomerPhone
                                    .Trim(),

                            CustomerEmail =
                                request
                                    .CustomerEmail
                                    .Trim(),

                            CustomerCCCD =
                                string.IsNullOrWhiteSpace(
                                    request.CustomerCCCD
                                )
                                ? null
                                : request
                                    .CustomerCCCD
                                    .Trim(),

                            CheckInDate =
                                request.CheckInDate,

                            CheckOutDate =
                                request.CheckOutDate,

                            GuestCount =
                                request.GuestCount,

                            RoomCount =
                                request.RoomCount,

                            SpecialRequest =
                                request.SpecialRequest,

                            PaymentMethod =
    paymentMethod,

                            TotalAmount =
                                totalAmount,

                            Status =
                                "Chờ xác nhận",

                            CreatedAt =
                                DateTime.Now
                        };


                    db.Bookings.Add(booking);

                    db.SaveChanges();

                    PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);


                    // =============================================
                    // LƯU CÁC DỊCH VỤ ĐÃ CHỌN
                    // =============================================

                    foreach (
                        var serviceRequest
                        in serviceRequests
                    )
                    {
                        var service =
                            services.First(
                                d =>
                                d.MaDichVu ==
                                serviceRequest.MaDichVu
                            );


                        var bookingService =
                            new DatPhongDichVu
                            {
                                BookingId =
                                    booking.BookingId,

                                MaDichVu =
                                    service.MaDichVu,

                                SoLuong =
                                    serviceRequest
                                        .SoLuong,

                                DonGia =
                                    service.Gia,

                                ThanhTien =
                                    service.Gia *
                                    serviceRequest
                                        .SoLuong
                            };


                        db.DatPhongDichVus.Add(
                            bookingService
                        );
                    }


                    // =============================================
                    // TẠO BẢN GHI THANH TOÁN (CỌC 40% NẾU THANH TOÁN TẠI KHÁCH SẠN)
                    // =============================================

                    decimal paymentAmount = totalAmount;
                    string paymentStatus = "Chờ thanh toán";
                    string paymentNote = "Đơn đặt phòng #" + booking.BookingId;

                    if (paymentMethod == "Thanh toán tại khách sạn" || paymentMethod.Contains("Thanh toán tại khách sạn"))
                    {
                        paymentAmount = Math.Round(totalAmount * 0.4m, 0);
                        paymentStatus = "Chờ thanh toán cọc";
                        paymentNote = "Cọc giữ phòng 40% (60% thanh toán tại quầy) cho đơn #" + booking.BookingId;
                    }

                    var payment =
                        new ThanhToan
                        {
                            BookingId =
                                booking.BookingId,

                            PhuongThucThanhToan =
                                paymentMethod,

                            SoTien =
                                paymentAmount,

                            NgayThanhToan =
                                DateTime.Now,

                            TrangThai =
                                paymentStatus,

                            MaGiaoDich =
                                null,

                            GhiChu =
                                paymentNote
                        };


                    db.ThanhToans.Add(
                        payment
                    );


                    // =============================================
                    // TĂNG SỐ LƯỢT DÙNG KHUYẾN MÃI
                    // =============================================

                    if (promotion != null)
                    {
                        promotion
                            .SoLuotDaSuDung += 1;
                    }


                    db.SaveChanges();


                    // =============================================
                    // LƯU UserId + MaKhuyenMai + TienGiam
                    //
                    // Dùng SQL trực tiếp để vẫn chạy được
                    // nếu Booking.cs của EDMX chưa sinh lại
                    // 3 thuộc tính này.
                    // =============================================

                    int? userId = null;


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


                    object promotionIdValue =
                        promotion != null

                        ? (object)
                          promotion.MaKhuyenMai

                        : DBNull.Value;


                    db.Database.ExecuteSqlCommand(
                        @"
                        UPDATE dbo.Bookings

                        SET
                            UserId = @p0,
                            MaKhuyenMai = @p1,
                            TienGiam = @p2

                        WHERE BookingId = @p3
                        ",

                        userIdValue,

                        promotionIdValue,

                        discountAmount,

                        booking.BookingId
                    );


                    transaction.Commit();
                    if (!userId.HasValue)
                    {
                        var owned = Session["GuestBookingIds"] as List<int> ?? new List<int>();
                        owned.Add(booking.BookingId);
                        Session["GuestBookingIds"] = owned;
                    }


                    // =============================================
                    // TRẢ VỀ KẾT QUẢ VÀ REDIRECT SANG TRANG THANH TOÁN
                    // =============================================

                    return Json(new
                    {
                        success = true,

                        bookingId =
                            booking.BookingId,

                        roomAmount =
                            roomAmount,

                        serviceAmount =
                            serviceAmount,

                        discountAmount =
                            discountAmount,

                        totalAmount =
                            totalAmount,

                        redirectUrl =
                            Url.Action("Payment", "Booking", new { id = booking.BookingId }),

                        message =
                            "Đặt phòng thành công. Đang chuyển đến trang thanh toán..."
                    });
                }
                catch (HotelRuleException ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = ex.Message });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    System.Diagnostics.Trace.TraceError(ex.ToString());


                    return Json(new
                    {
                        success = false,

                        message =
                            "Không thể lưu đặt phòng. Vui lòng kiểm tra thông tin hoặc thử lại sau."
                    });
                }
            }
        }


        // =========================================================
        // KIỂM TRA SỐ PHÒNG CÒN TRỐNG
        // =========================================================

        [HttpGet]
        public JsonResult CheckAvailability(
            int roomId,
            string checkIn,
            string checkOut)
        {
            DateTime start, end;
            if (!DateTime.TryParseExact(checkIn, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start) ||
                !DateTime.TryParseExact(checkOut, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end) ||
                start < DateTime.Today || end <= start)
                return Json(new { success = false, message = "Khoảng ngày không hợp lệ." }, JsonRequestBehavior.AllowGet);
            var room = db.Rooms.Find(roomId);
            if (room == null || !room.IsActive) return Json(new { success = false, message = "Không tìm thấy loại phòng." }, JsonRequestBehavior.AllowGet);
            int availableRooms = HotelOperations.AvailableRooms(db, roomId, start, end).Count;
            int totalRooms = db.PhysicalRooms.Count(p => p.RoomTypeId == roomId && p.IsActive);
            return Json(new { success = true, availableRooms, totalRooms }, JsonRequestBehavior.AllowGet);
        }


        // =========================================================
        // TRANG CHỌN PHƯƠNG THỨC THANH TOÁN
        // =========================================================
        private bool CanAccessBooking(int id)
        {
            int userId;
            if (int.TryParse(Convert.ToString(Session["UserId"]), out userId))
                if (db.Database.SqlQuery<int>("SELECT COUNT(1) FROM dbo.Bookings WHERE BookingId=@p0 AND UserId=@p1", id, userId).Single() == 1) return true;
            var owned = Session["GuestBookingIds"] as List<int>;
            return owned != null && owned.Contains(id);
        }

        private void CheckAndCancelIfExpired(Booking booking)
        {
            if (booking != null && (booking.Status == "Chờ xác nhận" || booking.Status == "Đã xác nhận") &&
                HotelOperations.PaidAmount(booking) < booking.TotalAmount &&
                (DateTime.Now - booking.CreatedAt).TotalMinutes > 10)
            {
                booking.Status = "Đã hủy";
                HotelOperations.ReturnPromotionUse(db, booking.BookingId);
                foreach (var p in db.ThanhToans.Where(x => x.BookingId == booking.BookingId && x.TrangThai != "Đã thanh toán").ToList())
                {
                    p.TrangThai = "Đã hủy";
                }
                booking.SpecialRequest = "[TỰ ĐỘNG HỦY DO QUÁ HẠN THANH TOÁN QR 10 PHÚT] " + (booking.SpecialRequest ?? "");
                db.SaveChanges();
                PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            }
        }

        [HttpGet]
        public ActionResult Payment(int id)
        {
            if (!CanAccessBooking(id)) return HttpNotFound();
            var booking = db.Bookings.Include("Room").Include("ThanhToans").FirstOrDefault(b => b.BookingId == id);
            if (booking == null) return HttpNotFound();
            CheckAndCancelIfExpired(booking);
            return View(booking);
        }

        // =========================================================
        // XÁC NHẬN THANH TOÁN TIỀN MẶT TẠI QUẦY
        // =========================================================
        [HttpPost]
        public JsonResult ConfirmCashPayment(int id)
        {
            if (!CanAccessBooking(id)) { Response.StatusCode = 403; return Json(new { success = false, message = "Không có quyền truy cập đơn." }); }
            var booking = db.Bookings.Find(id);
            if (booking == null || !HotelOperations.ActiveStatuses.Contains(booking.Status))
                return Json(new { success = false, message = "Không thể đổi phương thức thanh toán ở trạng thái hiện tại." });
            if (HotelOperations.PaidAmount(booking) >= booking.TotalAmount) return Json(new { success = false, message = "Đơn đã thanh toán đủ." });
            booking.PaymentMethod = "Thanh toán tại khách sạn";
            var payment = HotelOperations.SynchronizeOutstandingPayment(db, booking);
            if (payment == null) return Json(new { success = false, message = "Đơn đã thanh toán đủ." });
            payment.PhuongThucThanhToan = booking.PaymentMethod;
            payment.TrangThai = "Chờ thanh toán tại quầy";
            db.SaveChanges();
            return Json(new { success = true, redirectUrl = Url.Action("Success", new { id }) });
        }

        // =========================================================
        // KIỂM TRA TRẠNG THÁI THANH TOÁN (AJAX POLLING)
        // =========================================================
        [HttpGet]
        public JsonResult CheckPaymentStatus(int id)
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            if (!CanAccessBooking(id)) { Response.StatusCode = 403; return Json(new { success = false, isPaid = false }, JsonRequestBehavior.AllowGet); }
            var booking = db.Bookings.Include("ThanhToans").FirstOrDefault(b => b.BookingId == id);
            if (booking == null) return Json(new { success = false, isPaid = false }, JsonRequestBehavior.AllowGet);
            CheckAndCancelIfExpired(booking);
            decimal paid = db.ThanhToans.Where(p => p.BookingId == id && p.TrangThai == "Đã thanh toán").Select(p => (decimal?)p.SoTien).Sum() ?? 0;
            return Json(new { success = booking != null, isPaid = booking != null && paid >= booking.TotalAmount,
                canPay = booking != null && HotelOperations.ActiveStatuses.Contains(booking.Status),
                outstandingAmount = booking == null ? 0 : Math.Max(0, booking.TotalAmount - paid),
                needsReview = booking != null && QLKhachSan.Services.SePayPaymentService.NeedsReview(db, "Booking", id),
                status = booking == null ? "" : booking.Status }, JsonRequestBehavior.AllowGet);
        }

        // =========================================================
        // XÁC NHẬN / MÔ PHỎNG CHUYỂN KHOẢN THÀNH CÔNG
        // =========================================================
        [HttpPost]
        public JsonResult SimulateBankTransferPaid(int id)
        {
            Response.StatusCode = 403;
            return Json(new { success = false, message = "Không ghi nhận giao dịch mô phỏng. Vui lòng chờ nhân viên xác minh thanh toán." });
        }

        // =========================================================
        // TRANG THÀNH CÔNG (SUCCESS)
        // =========================================================
        [HttpPost]
        public JsonResult RequestBankTransferReview(int id)
        {
            if (!CanAccessBooking(id)) { Response.StatusCode = 403; return Json(new { success = false, message = "Không có quyền truy cập đơn." }); }
            var booking = db.Bookings.Find(id);
            if (booking == null || !HotelOperations.ActiveStatuses.Contains(booking.Status))
                return Json(new { success = false, message = "Đơn không còn nhận thanh toán." });
            if (HotelOperations.PaidAmount(booking) >= booking.TotalAmount) return Json(new { success = false, message = "Đơn đã thanh toán đủ." });
            booking.PaymentMethod = "Chuyển khoản";
            var payment = HotelOperations.SynchronizeOutstandingPayment(db, booking);
            if (payment == null) return Json(new { success = false, message = "Đơn đã thanh toán đủ." });
            payment.PhuongThucThanhToan = "Chuyển khoản";
            payment.TrangThai = "Chờ xác minh";
            payment.GhiChu = "Khách yêu cầu đối soát chuyển khoản; chưa xác nhận đã nhận tiền.";
            db.SaveChanges();
            return Json(new { success = true, redirectUrl = Url.Action("Success", new { id }) });
        }

        [HttpGet]
        public ActionResult Success(int id)
        {
            if (!CanAccessBooking(id)) return HttpNotFound();
            var booking = db.Bookings.Include("Room").Include("ThanhToans").FirstOrDefault(b => b.BookingId == id);
            if (booking == null) return HttpNotFound();
            return View(booking);
        }


        [HttpGet]
        public JsonResult GetAvailablePhysicalRooms(int roomId, DateTime checkIn, DateTime checkOut)
        {
            if (!ModelState.IsValid || checkIn.Date < DateTime.Today || checkOut.Date <= checkIn.Date)
                return Json(new { success = false, message = "Khoảng ngày không hợp lệ." }, JsonRequestBehavior.AllowGet);
            try
            {
                var available = new HashSet<int>(HotelOperations.AvailableRooms(db, roomId, checkIn.Date, checkOut.Date).Select(p => p.PhysicalRoomId));
                var physicalRooms = db.PhysicalRooms.AsNoTracking().Where(p => p.RoomTypeId == roomId && p.IsActive && p.Room.IsActive)
                    .OrderBy(p => p.RoomNumber).ToList().Select(p => new { physicalRoomId = p.PhysicalRoomId,
                        roomNumber = p.RoomNumber, floor = p.Floor, isBooked = !available.Contains(p.PhysicalRoomId) }).ToList();
                return Json(new { success = true, physicalRooms }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(new { success = false, message = "Không tải được số phòng. Vui lòng thử lại hoặc liên hệ khách sạn." }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpPost]
        public JsonResult ProcessOverdueBookings(string apiKey)
        {
            string expectedKey = ConfigurationManager.AppSettings["OverdueJobApiKey"] ?? "LuxuryHotelCronKey2026";
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey != expectedKey)
            {
                return Json(new { success = false, message = "Khóa API không hợp lệ." }, JsonRequestBehavior.AllowGet);
            }

            int count = OverdueBookingService.ProcessOverdueBookings(db);
            return Json(new { success = true, message = $"Đã tự động xử lý {count} đơn quá hạn nhận phòng.", processedCount = count }, JsonRequestBehavior.AllowGet);
        }

        // =========================================================
        // GIẢI PHÓNG DB CONTEXT
        // =========================================================

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
