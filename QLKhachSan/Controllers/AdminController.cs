using QLKhachSan.Filters;
using QLKhachSan.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Web.Helpers;
using System.Web.Mvc;

namespace QLKhachSan.Controllers
{
    [AdminAuthorize]
    public class AdminController : HotelController
    {
// =========================================================
        // DASHBOARD VẬN HÀNH NÂNG CAO
        // =========================================================
        // DASHBOARD QUẢN TRỊ / TỔNG QUAN
        // =========================================================
        public ActionResult Index(
            string keyword,
            string status,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = db.Bookings
                .Include(b => b.Room)
                .Include(b => b.ThanhToans)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(b =>
                    b.CustomerName.Contains(keyword) ||
                    b.CustomerPhone.Contains(keyword) ||
                    b.CustomerEmail.Contains(keyword) ||
                    (b.SpecialRequest != null && b.SpecialRequest.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(b => b.Status == status);
            }

            if (fromDate.HasValue)
            {
                DateTime start = fromDate.Value.Date;
                query = query.Where(b => b.CreatedAt >= start);
            }

            if (toDate.HasValue)
            {
                DateTime end = toDate.Value.Date.AddDays(1);
                query = query.Where(b => b.CreatedAt < end);
            }

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);
            DateTime monthStart = new DateTime(today.Year, today.Month, 1);
            DateTime monthEnd = monthStart.AddMonths(1);

            decimal roomToday = HotelOperations.RoomRevenue(db, today, tomorrow);

            decimal roomMonth = HotelOperations.RoomRevenue(db, monthStart, monthEnd);

            decimal foodToday = GetFoodRevenue(today, tomorrow);

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate.HasValue ? fromDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.ToDate = toDate.HasValue ? toDate.Value.ToString("yyyy-MM-dd") : "";

            ViewBag.TodayRevenue = roomToday + foodToday;
            ViewBag.MonthRevenue = roomMonth + GetFoodRevenue(monthStart, monthEnd);
            ViewBag.TodayRoomRevenue = roomToday;
            ViewBag.TodayFoodRevenue = foodToday;

            ViewBag.PendingFoodOrders = GetPendingFoodOrders();
            ViewBag.TotalBookings = db.Bookings.Count();
            ViewBag.PendingBookings = db.Bookings.Count(b => b.Status == "Chờ xác nhận");
            ViewBag.CurrentGuests = db.Bookings.Count(b => b.Status == "Đang ở");

            // Doanh thu 7 ngày gần nhất cho biểu đồ
            var revenueLabels = new List<string>();
            var roomRevenue7Days = new List<decimal>();
            var foodRevenue7Days = new List<decimal>();

            for (int i = 6; i >= 0; i--)
            {
                DateTime d = today.AddDays(-i);
                DateTime dStart = d.Date;
                DateTime dEnd = d.Date.AddDays(1);

                revenueLabels.Add(d.ToString("dd/MM"));

                decimal rRev = HotelOperations.RoomRevenue(db, dStart, dEnd);

                decimal fRev = GetFoodRevenue(dStart, dEnd);

                roomRevenue7Days.Add(rRev);
                foodRevenue7Days.Add(fRev);
            }

            ViewBag.RevenueLabels = revenueLabels;
            ViewBag.RoomRevenue7Days = roomRevenue7Days;
            ViewBag.FoodRevenue7Days = foodRevenue7Days;

            var bookingsList = query.OrderByDescending(b => b.CreatedAt).ToList();
            return View(bookingsList);
        }



        // =========================================================
        // QUẢN LÝ ĐẶT PHÒNG
        // =========================================================
        public ActionResult Bookings(
            string keyword,
            string status,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = db.Bookings
                .Include(b => b.Room)
                .Include(b => b.ThanhToans)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(b =>
                    b.CustomerName.Contains(keyword) ||
                    b.CustomerPhone.Contains(keyword) ||
                    b.CustomerEmail.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(b => b.Status == status);
            }

            if (fromDate.HasValue)
            {
                DateTime start = fromDate.Value.Date;
                query = query.Where(b => b.CreatedAt >= start);
            }

            if (toDate.HasValue)
            {
                DateTime end = toDate.Value.Date.AddDays(1);
                query = query.Where(b => b.CreatedAt < end);
            }

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate.HasValue ? fromDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.ToDate = toDate.HasValue ? toDate.Value.ToString("yyyy-MM-dd") : "";
            ViewBag.TotalBookings = db.Bookings.Count();
            ViewBag.PendingBookings = db.Bookings.Count(b => b.Status == "Chờ xác nhận");
            ViewBag.CurrentGuests = db.Bookings.Count(b => b.Status == "Đang ở");
            ViewBag.CheckedOutBookings = db.Bookings.Count(b => b.Status == "Đã trả phòng");

            return View(query.OrderByDescending(b => b.CreatedAt).ToList());
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult Confirm(int id, string returnTo = null)
        {
            var booking = db.Bookings.Find(id);
            if (booking == null)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Không tìm thấy đơn đặt phòng." });
                return HttpNotFound();
            }

            if (booking.Status != "Chờ xác nhận")
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Chỉ có thể xác nhận đơn đang chờ xác nhận." });
                FailOperation(); TempData["AdminError"] = "Chỉ có thể xác nhận đơn đang chờ xác nhận.";
                return RedirectAfterBookingAction(returnTo);
            }

            try { HotelOperations.EnsureReservation(db, booking); }
            catch (HotelRuleException ex)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = ex.Message });
                FailOperation(); TempData["AdminError"] = ex.Message;
                return RedirectAfterBookingAction(returnTo);
            }
            booking.Status = "Đã xác nhận";
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = $"Đã xác nhận đơn #{booking.BookingId}.", status = booking.Status });
            }

            TempData["AdminSuccess"] = "Đã xác nhận đơn #" + booking.BookingId + ".";
            return RedirectAfterBookingAction(returnTo);
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult Cancel(int id, string returnTo = null)
        {
            var booking = db.Bookings.Find(id);
            if (booking == null)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Không tìm thấy đơn đặt phòng." });
                return HttpNotFound();
            }

            if (booking.Status != "Chờ xác nhận" && booking.Status != "Đã xác nhận")
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Không thể hủy đơn ở trạng thái hiện tại." });
                FailOperation(); TempData["AdminError"] = "Không thể hủy đơn ở trạng thái hiện tại.";
                return RedirectAfterBookingAction(returnTo);
            }

            bool paid = db.ThanhToans.Any(t =>
                t.BookingId == id && t.TrangThai == "Đã thanh toán");

            if (paid)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Đơn đã thanh toán. Không thể hủy trực tiếp." });
                FailOperation(); TempData["AdminError"] = "Đơn đã thanh toán. Không thể hủy trực tiếp.";
                return RedirectAfterBookingAction(returnTo);
            }

            HotelOperations.ReturnPromotionUse(db, id);
            booking.Status = "Đã hủy";
            foreach (var p in db.ThanhToans.Where(t => t.BookingId == id).ToList())
            {
                if (p.TrangThai != "Đã thanh toán") p.TrangThai = "Đã hủy";
            }

            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = $"Đã hủy đơn #{booking.BookingId}.", status = booking.Status });
            }

            TempData["AdminSuccess"] = "Đã hủy đơn #" + booking.BookingId + ".";
            return RedirectAfterBookingAction(returnTo);
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult ConfirmPayment(int id, string returnTo = null)
        {
            var booking = db.Bookings.Find(id);
            if (booking == null) return HttpNotFound();
            try
            {
                HotelOperations.ConfirmBookingPayment(db, booking, Request.Headers["X-Payment-Evidence"] ?? Request["paymentEvidence"], Convert.ToInt32(Session["UserId"]));
            }
            catch (HotelRuleException ex)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = ex.Message });
                FailOperation(); TempData["AdminError"] = ex.Message;
                return RedirectAfterBookingAction(returnTo);
            }
            if (Request.IsAjaxRequest()) return Json(new { success = true, message = "Đã xác minh và ghi nhận thanh toán.", status = booking.Status });
            TempData["AdminSuccess"] = "Đã xác minh và ghi nhận thanh toán.";
            return RedirectAfterBookingAction(returnTo);
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult CheckIn(int id, string returnTo = null)
        {
            var booking = db.Bookings.Find(id);
            if (booking == null) return HttpNotFound();
            try { HotelOperations.ValidateCheckIn(db, booking); }
            catch (HotelRuleException ex)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = ex.Message });
                FailOperation(); TempData["AdminError"] = ex.Message;
                return RedirectAfterBookingAction(returnTo);
            }
            booking.Status = "Đang ở";
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            if (Request.IsAjaxRequest()) return Json(new { success = true, message = "Đã nhận phòng.", status = booking.Status });
            TempData["AdminSuccess"] = "Đã nhận phòng.";
            return RedirectAfterBookingAction(returnTo);
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult CheckOut(int id, string returnTo = null)
        {
            var booking = db.Bookings.Find(id);
            if (booking == null)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Không tìm thấy đơn đặt phòng." });
                return HttpNotFound();
            }

            if (booking.Status != "Đang ở")
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Chỉ khách đang ở mới có thể trả phòng." });
                FailOperation(); TempData["AdminError"] = "Chỉ khách đang ở mới có thể trả phòng.";
                return RedirectAfterBookingAction(returnTo);
            }

            decimal paidAmount = db.ThanhToans.Where(t => t.BookingId == id && t.TrangThai == "Đã thanh toán").Select(t => (decimal?)t.SoTien).Sum() ?? 0;

            if (paidAmount < booking.TotalAmount)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Khách chưa thanh toán. Vui lòng xác nhận thanh toán trước." });
                FailOperation(); TempData["AdminError"] = "Khách chưa thanh toán. Vui lòng xác nhận thanh toán trước.";
                return RedirectAfterBookingAction(returnTo);
            }

            booking.Status = "Đã trả phòng";
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = $"Đã trả phòng cho đơn #{booking.BookingId}.", status = booking.Status });
            }

            TempData["AdminSuccess"] = "Đã trả phòng cho đơn #" + booking.BookingId + ".";
            return RedirectAfterBookingAction(returnTo);
        }

        private ActionResult RedirectAfterBookingAction(string returnTo)
        {
            if (returnTo == "CurrentGuests") return RedirectToAction("CurrentGuests");
            return RedirectToAction("Bookings");
        }

        // =========================================================
        // QUẢN LÝ PHÒNG
        // =========================================================
        public ActionResult Rooms(string keyword, string status)
        {
            var query = db.Rooms.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(r =>
                    r.RoomName.Contains(keyword) ||
                    r.RoomType.Contains(keyword));
            }

            if (status == "active") query = query.Where(r => r.IsActive);
            else if (status == "inactive") query = query.Where(r => !r.IsActive);

            ViewBag.Keyword = keyword;
            ViewBag.RoomStatus = status;
            ViewBag.TotalRoomTypes = db.Rooms.Count();
            ViewBag.ActiveRoomTypes = db.Rooms.Count(r => r.IsActive);
            ViewBag.InactiveRoomTypes = db.Rooms.Count(r => !r.IsActive);
            ViewBag.TotalRoomUnits = db.Rooms.Where(r => r.IsActive).Select(r => (int?)r.TotalRooms).Sum() ?? 0;
            ViewBag.PhysicalRooms = HotelOperations.RoomSnapshot(db, DateTime.Today);

            return View(query.OrderBy(r => r.Price).ToList());
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult SaveRoom(
            int? id,
            string roomName,
            string roomType,
            decimal price,
            int capacity,
            int totalRooms,
            string description,
            string imageUrl,
            bool isActive = true)
        {
            roomName = (roomName ?? "").Trim();
            roomType = (roomType ?? "").Trim();
            description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            imageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(roomType) || price < 0 || capacity <= 0 || totalRooms < 0)
            {
                FailOperation(); TempData["AdminError"] = "Thông tin phòng không hợp lệ.";
                return RedirectToAction("Rooms");
            }

            int physicalCount = id.HasValue ? db.PhysicalRooms.Count(p => p.RoomTypeId == id.Value) : 0;
            if (id.HasValue && totalRooms != physicalCount)
            {
                FailOperation(); TempData["AdminError"] = "Số lượng phải khớp " + physicalCount + " phòng vật lý.";
                return RedirectToAction("Rooms");
            }
            if (!id.HasValue) totalRooms = 0; // A new type has no sellable physical rooms yet.
            bool duplicate = id.HasValue
                ? db.Rooms.Any(r => r.RoomName == roomName && r.RoomId != id.Value)
                : db.Rooms.Any(r => r.RoomName == roomName);

            if (duplicate)
            {
                FailOperation(); TempData["AdminError"] = "Tên phòng này đã tồn tại.";
                return RedirectToAction("Rooms");
            }

            if (id.HasValue)
            {
                var room = db.Rooms.Find(id.Value);
                if (room == null) return HttpNotFound();

                room.RoomName = roomName;
                room.RoomType = roomType;
                room.Price = price;
                room.Capacity = capacity;
                room.TotalRooms = totalRooms;
                room.Description = description;
                room.ImageUrl = imageUrl;
                room.IsActive = isActive;
            }
            else
            {
                db.Rooms.Add(new Room
                {
                    RoomName = roomName,
                    RoomType = roomType,
                    Price = price,
                    Capacity = capacity,
                    TotalRooms = totalRooms,
                    Description = description,
                    ImageUrl = imageUrl,
                    IsActive = isActive
                });
            }

            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = id.HasValue ? "Đã cập nhật phòng." : "Đã thêm phòng mới.";
            return RedirectToAction("Rooms");
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult ToggleRoom(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();

            room.IsActive = !room.IsActive;
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = room.IsActive ? "Đã bật hoạt động phòng." : "Đã ngừng hoạt động phòng.";
            return RedirectToAction("Rooms");
        }

        // =========================================================
        // KHÁCH ĐANG Ở
        // =========================================================
        public ActionResult CurrentGuests()
        {
            var bookings = db.Bookings
                .Include(b => b.Room)
                .Include(b => b.ThanhToans)
                .Where(b => b.Status == "Đang ở")
                .OrderBy(b => b.CheckOutDate)
                .ToList();

            ViewBag.CurrentGuestCount = bookings.Count;
            ViewBag.UnpaidGuests = bookings.Count(b =>
                HotelOperations.PaymentStatus(b) != "Đã thanh toán");

            return View(bookings);
        }

        // =========================================================
        // TÀI KHOẢN
        // =========================================================
        public ActionResult Users(string keyword, string role)
        {
            var query = db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(u =>
                    u.FullName.Contains(keyword) ||
                    u.Email.Contains(keyword) ||
                    u.Username.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Role == role);

            ViewBag.Keyword = keyword;
            ViewBag.RoleFilter = role;
            ViewBag.TotalUsers = db.Users.Count();
            ViewBag.CustomerCount = db.Users.Count(u => u.Role == "Customer");
            ViewBag.StaffCount = db.Users.Count(u => u.Role == "Staff");
            ViewBag.ActiveUsers = db.Users.Count(u => u.IsActive);

            return View(query.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToList());
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult CreateStaff(string fullName, string email, string password)
        {
            fullName = (fullName ?? "").Trim();
            email = (email ?? "").Trim().ToLowerInvariant();
            password = password ?? "";

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || password.Length < 6)
            {
                FailOperation(); TempData["AdminError"] = "Thông tin chưa hợp lệ. Mật khẩu ít nhất 6 ký tự.";
                return RedirectToAction("Users", new { role = "Staff" });
            }

            if (db.Users.Any(u => u.Email == email || u.Username == email))
            {
                FailOperation(); TempData["AdminError"] = "Email này đã được sử dụng.";
                return RedirectToAction("Users", new { role = "Staff" });
            }

            db.Users.Add(new User
            {
                Username = email,
                Email = email,
                FullName = fullName,
                PasswordHash = Crypto.HashPassword(password),
                Role = "Staff",
                IsActive = true,
                CreatedAt = DateTime.Now
            });

            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = "Đã tạo tài khoản nhân viên.";
            return RedirectToAction("Users", new { role = "Staff" });
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult ToggleUser(int id)
        {
            var user = db.Users.Find(id);
            if (user == null) return HttpNotFound();

            if (user.Role == "Admin")
            {
                FailOperation(); TempData["AdminError"] = "Không thể khóa tài khoản Admin tại đây.";
                return RedirectToAction("Users");
            }

            user.IsActive = !user.IsActive;
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = user.IsActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.";
            return RedirectToAction("Users");
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult ChangeUserRole(int id, string role)
        {
            var user = db.Users.Find(id);
            if (user == null) return HttpNotFound();

            if (user.Role == "Admin")
            {
                FailOperation(); TempData["AdminError"] = "Không thể thay đổi quyền Admin.";
                return RedirectToAction("Users");
            }

            if (role != "Customer" && role != "Staff")
            {
                FailOperation(); TempData["AdminError"] = "Quyền không hợp lệ.";
                return RedirectToAction("Users");
            }

            user.Role = role;
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = "Đã cập nhật quyền tài khoản.";
            return RedirectToAction("Users");
        }

        // =========================================================
        // DỊCH VỤ
        // =========================================================
        public ActionResult Services(string keyword)
        {
            var query = db.DichVus.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(d => d.TenDichVu.Contains(keyword) || d.MoTa.Contains(keyword));
            }

            ViewBag.Keyword = keyword;
            ViewBag.TotalServices = db.DichVus.Count();
            ViewBag.ActiveServices = db.DichVus.Count(d => d.HoatDong);

            return View(query.OrderBy(d => d.TenDichVu).ToList());
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult SaveService(int? id, string tenDichVu, string moTa, decimal gia, string donVi, bool hoatDong = true)
        {
            tenDichVu = (tenDichVu ?? "").Trim();
            donVi = (donVi ?? "").Trim();
            moTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim();

            if (string.IsNullOrWhiteSpace(tenDichVu) || string.IsNullOrWhiteSpace(donVi) || gia < 0)
            {
                FailOperation(); TempData["AdminError"] = "Thông tin dịch vụ không hợp lệ.";
                return RedirectToAction("Services");
            }

            if (id.HasValue)
            {
                var service = db.DichVus.Find(id.Value);
                if (service == null) return HttpNotFound();
                service.TenDichVu = tenDichVu;
                service.MoTa = moTa;
                service.Gia = gia;
                service.DonVi = donVi;
                service.HoatDong = hoatDong;
            }
            else
            {
                db.DichVus.Add(new DichVu
                {
                    TenDichVu = tenDichVu,
                    MoTa = moTa,
                    Gia = gia,
                    DonVi = donVi,
                    HoatDong = hoatDong
                });
            }

            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = id.HasValue ? "Đã cập nhật dịch vụ." : "Đã thêm dịch vụ.";
            return RedirectToAction("Services");
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult ToggleService(int id)
        {
            var service = db.DichVus.Find(id);
            if (service == null) return HttpNotFound();
            service.HoatDong = !service.HoatDong;
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = service.HoatDong ? "Đã bật dịch vụ." : "Đã ngừng dịch vụ.";
            return RedirectToAction("Services");
        }

        // =========================================================
        // KHUYẾN MÃI
        // =========================================================
        public ActionResult Promotions(string keyword)
        {
            var query = db.KhuyenMais.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(k => k.MaCode.Contains(keyword) || k.TenKhuyenMai.Contains(keyword));
            }

            DateTime today = DateTime.Today;
            ViewBag.Keyword = keyword;
            ViewBag.TotalPromotions = db.KhuyenMais.Count();
            ViewBag.ActivePromotions = db.KhuyenMais.Count(k =>
                k.HoatDong && k.NgayBatDau <= today && k.NgayKetThuc >= today);

            return View(query.OrderByDescending(k => k.NgayBatDau).ToList());
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult SavePromotion(
            int? id,
            string maCode,
            string tenKhuyenMai,
            string moTa,
            string loaiGiamGia,
            decimal giaTriGiam,
            decimal donToiThieu,
            decimal? giamToiDa,
            DateTime ngayBatDau,
            DateTime ngayKetThuc,
            int? soLuotSuDungToiDa,
            bool hoatDong = true)
        {
            maCode = (maCode ?? "").Trim().ToUpperInvariant();
            tenKhuyenMai = (tenKhuyenMai ?? "").Trim();
            moTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim();

            if (string.IsNullOrWhiteSpace(maCode) || string.IsNullOrWhiteSpace(tenKhuyenMai) || giaTriGiam <= 0 || donToiThieu < 0 || (giamToiDa.HasValue && giamToiDa.Value < 0) || (soLuotSuDungToiDa.HasValue && soLuotSuDungToiDa.Value < 1) || ngayKetThuc.Date < ngayBatDau.Date)
            {
                FailOperation(); TempData["AdminError"] = "Thông tin khuyến mãi không hợp lệ.";
                return RedirectToAction("Promotions");
            }

            if (loaiGiamGia != "PhanTram" && loaiGiamGia != "SoTien")
            {
                FailOperation(); TempData["AdminError"] = "Loại giảm giá không hợp lệ.";
                return RedirectToAction("Promotions");
            }

            if (loaiGiamGia == "PhanTram" && giaTriGiam > 100)
            {
                FailOperation(); TempData["AdminError"] = "Phần trăm giảm không được lớn hơn 100%.";
                return RedirectToAction("Promotions");
            }

            bool duplicate = id.HasValue
                ? db.KhuyenMais.Any(k => k.MaCode == maCode && k.MaKhuyenMai != id.Value)
                : db.KhuyenMais.Any(k => k.MaCode == maCode);

            if (duplicate)
            {
                FailOperation(); TempData["AdminError"] = "Mã khuyến mãi đã tồn tại.";
                return RedirectToAction("Promotions");
            }

            if (id.HasValue)
            {
                var p = db.KhuyenMais.Find(id.Value);
                if (p == null) return HttpNotFound();
                p.MaCode = maCode;
                p.TenKhuyenMai = tenKhuyenMai;
                p.MoTa = moTa;
                p.LoaiGiamGia = loaiGiamGia;
                p.GiaTriGiam = giaTriGiam;
                p.DonToiThieu = donToiThieu;
                p.GiamToiDa = giamToiDa;
                p.NgayBatDau = ngayBatDau.Date;
                p.NgayKetThuc = ngayKetThuc.Date;
                p.SoLuotSuDungToiDa = soLuotSuDungToiDa;
                p.HoatDong = hoatDong;
            }
            else
            {
                db.KhuyenMais.Add(new KhuyenMai
                {
                    MaCode = maCode,
                    TenKhuyenMai = tenKhuyenMai,
                    MoTa = moTa,
                    LoaiGiamGia = loaiGiamGia,
                    GiaTriGiam = giaTriGiam,
                    DonToiThieu = donToiThieu,
                    GiamToiDa = giamToiDa,
                    NgayBatDau = ngayBatDau.Date,
                    NgayKetThuc = ngayKetThuc.Date,
                    SoLuotSuDungToiDa = soLuotSuDungToiDa,
                    SoLuotDaSuDung = 0,
                    HoatDong = hoatDong
                });
            }

            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = id.HasValue ? "Đã cập nhật khuyến mãi." : "Đã thêm khuyến mãi.";
            return RedirectToAction("Promotions");
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult TogglePromotion(int id)
        {
            var p = db.KhuyenMais.Find(id);
            if (p == null) return HttpNotFound();
            p.HoatDong = !p.HoatDong;
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = p.HoatDong ? "Đã bật khuyến mãi." : "Đã tắt khuyến mãi.";
            return RedirectToAction("Promotions");
        }

        // =========================================================
        // DOANH THU
        // =========================================================
        public ActionResult Revenue(int? year)
        {
            int selectedYear = year ?? DateTime.Today.Year;
            if (selectedYear < 2000 || selectedYear > 2100) selectedYear = DateTime.Today.Year;

            string[] monthLabels =
            {
                "Tháng 1","Tháng 2","Tháng 3","Tháng 4","Tháng 5","Tháng 6",
                "Tháng 7","Tháng 8","Tháng 9","Tháng 10","Tháng 11","Tháng 12"
            };

            decimal[] roomRevenueData = new decimal[12];
            decimal[] foodRevenueData = new decimal[12];
            decimal[] totalRevenueData = new decimal[12];

            for (int month = 1; month <= 12; month++)
            {
                DateTime start = new DateTime(selectedYear, month, 1);
                DateTime end = start.AddMonths(1);

                decimal roomRevenue = HotelOperations.RoomRevenue(db, start, end);

                decimal foodRevenue = GetFoodRevenue(start, end);

                roomRevenueData[month - 1] = roomRevenue;
                foodRevenueData[month - 1] = foodRevenue;
                totalRevenueData[month - 1] = roomRevenue + foodRevenue;
            }

            DateTime yearStart = new DateTime(selectedYear, 1, 1);
            DateTime yearEnd = yearStart.AddYears(1);

            ViewBag.SelectedYear = selectedYear;
            ViewBag.MonthLabels = monthLabels;
            ViewBag.RoomRevenueData = roomRevenueData;
            ViewBag.FoodRevenueData = foodRevenueData;
            ViewBag.TotalRevenueData = totalRevenueData;
            ViewBag.RoomYearRevenue = roomRevenueData.Sum();
            ViewBag.FoodYearRevenue = foodRevenueData.Sum();
            ViewBag.YearRevenue = roomRevenueData.Sum() + foodRevenueData.Sum();
            ViewBag.PaidRoomPayments = db.ThanhToans.Count(t => t.TrangThai == "Đã thanh toán" && t.NgayThanhToan >= yearStart && t.NgayThanhToan < yearEnd);
            ViewBag.PaidFoodOrders = GetPaidFoodOrders(yearStart, yearEnd);

            return View();
        }

        // =========================================================
        // LỊCH PHÒNG
        // =========================================================
        public ActionResult RoomCalendar(
            DateTime? date)
        {
            DateTime selectedDate =
                date.HasValue
                ? date.Value.Date
                : DateTime.Today;

            DateTime nextDate =
                selectedDate.AddDays(1);

            var rooms =
                db.Rooms
                  .Where(r => r.IsActive)
                  .OrderBy(r => r.RoomName)
                  .ToList();

            var bookings =
                db.Bookings
                  .Include(b => b.Room)
                  .Where(b =>
                      b.Status != "Đã hủy" &&
                      b.CheckInDate < nextDate &&
                      b.CheckOutDate > selectedDate)
                  .OrderBy(b =>
                      b.CheckInDate)
                  .ToList();

            var model =
                new RoomCalendarPageViewModel
                {
                    SelectedDate =
                        selectedDate,

                    Rooms =
                        BuildRoomAvailability(
                            rooms,
                            bookings, selectedDate
                        )
                };

            return View(model);
        }


        // =========================================================
        // CHI TIẾT BOOKING
        // =========================================================
        public class HotelAutoCancelAuditModel
        {
            public long AuditId { get; set; }
            public int BookingId { get; set; }
            public string PreviousStatus { get; set; }
            public string NewStatus { get; set; }
            public DateTime CheckInDate { get; set; }
            public DateTime ProcessedAtUtc { get; set; }
            public bool HadPaidPayment { get; set; }
            public bool NeedsFinancialReview { get; set; }
            public string Reason { get; set; }
        }

        public ActionResult BookingDetails(int id)
        {
            var booking =
                db.Bookings
                  .Include(b => b.Room)
                  .Include(b => b.ThanhToans)
                  .FirstOrDefault(b =>
                      b.BookingId == id);

            if (booking == null)
            {
                return HttpNotFound();
            }

            var audit = db.Database.SqlQuery<HotelAutoCancelAuditModel>(
                "SELECT * FROM dbo.HotelAutoCancelAudit WHERE BookingId = @p0", id).FirstOrDefault();
            ViewBag.AutoCancelAudit = audit;

            var model =
                new BookingDetailsViewModel
                {
                    Booking =
                        booking
                };

            return View(model);
        }


        // =========================================================
        // LỊCH SỬ THANH TOÁN
        // =========================================================
        public ActionResult Payments(
            string keyword,
            string status,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var bookingQuery =
                db.Bookings
                  .Include(b => b.Room)
                  .Include(b => b.ThanhToans)
                  .AsQueryable();

            if (
                !string.IsNullOrWhiteSpace(
                    keyword
                )
            )
            {
                keyword =
                    keyword.Trim();

                bookingQuery =
                    bookingQuery.Where(b =>
                        b.CustomerName.Contains(
                            keyword
                        ) ||
                        b.CustomerPhone.Contains(
                            keyword
                        ) ||
                        b.CustomerEmail.Contains(
                            keyword
                        ));
            }

            var bookings =
                bookingQuery.ToList();

            var paymentRows =
                new List<AdminPaymentViewModel>();

            foreach (
                var booking
                in bookings
            )
            {
                foreach (
                    var payment
                    in booking.ThanhToans
                )
                {
                    if (
                        !string.IsNullOrWhiteSpace(
                            status
                        ) &&
                        payment.TrangThai !=
                        status
                    )
                    {
                        continue;
                    }

                    if (
                        fromDate.HasValue &&
                        payment.NgayThanhToan <
                        fromDate.Value.Date
                    )
                    {
                        continue;
                    }

                    if (
                        toDate.HasValue &&
                        payment.NgayThanhToan >=
                        toDate.Value.Date.AddDays(1)
                    )
                    {
                        continue;
                    }

                    paymentRows.Add(
                        new AdminPaymentViewModel
                        {
                            BookingId =
                                booking.BookingId,

                            CustomerName =
                                booking.CustomerName,

                            CustomerPhone =
                                booking.CustomerPhone,

                            CustomerEmail =
                                booking.CustomerEmail,

                            RoomName =
                                booking.Room != null
                                ? booking.Room.RoomName
                                : "",

                            Amount =
                                payment.SoTien,

                            Method =
                                payment.PhuongThucThanhToan,

                            Status =
                                payment.TrangThai,

                            PaidAt =
                                payment.NgayThanhToan,

                            TransactionCode =
                                payment.MaGiaoDich,

                            Note =
                                payment.GhiChu
                        }
                    );
                }
            }

            paymentRows =
                paymentRows
                    .OrderByDescending(x =>
                        x.PaidAt)
                    .ToList();

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

            ViewBag.TotalPayments =
                paymentRows.Count;

            ViewBag.PaidAmount =
                paymentRows
                    .Where(x =>
                        x.Status ==
                        "Đã thanh toán")
                    .Sum(x =>
                        x.Amount);

            return View(paymentRows);
        }


        // =========================================================
        // TRUNG TÂM THÔNG BÁO ADMIN
        // =========================================================
        public ActionResult Notifications()
        {
            DateTime today =
                DateTime.Today;

            DateTime tomorrow =
                today.AddDays(1);

            var model =
                new AdminNotificationsViewModel();

            var pendingBookings =
                db.Bookings
                  .Include(b => b.Room)
                  .Where(b =>
                      b.Status ==
                      "Chờ xác nhận")
                  .OrderBy(b =>
                      b.CreatedAt)
                  .Take(20)
                  .ToList();

            foreach (
                var booking
                in pendingBookings
            )
            {
                model.Items.Add(
                    new AdminNotificationItemViewModel
                    {
                        Type =
                            "Booking",

                        Level =
                            "warning",

                        Icon =
                            "fa-clock",

                        Title =
                            "Đơn #" +
                            booking.BookingId +
                            " đang chờ xác nhận",

                        Description =
                            booking.CustomerName +
                            " • " +
                            (
                                booking.Room != null
                                ? booking.Room.RoomName
                                : "Phòng"
                            ),

                        TimeText =
                            booking.CreatedAt.ToString(
                                "dd/MM/yyyy HH:mm"
                            ),

                        BookingId =
                            booking.BookingId
                    }
                );
            }

            var arrivals =
                db.Bookings
                  .Include(b => b.Room)
                  .Where(b =>
                      b.Status ==
                      "Đã xác nhận" &&
                      b.CheckInDate >= today &&
                      b.CheckInDate < tomorrow)
                  .OrderBy(b =>
                      b.CheckInDate)
                  .ToList();

            foreach (
                var booking
                in arrivals
            )
            {
                model.Items.Add(
                    new AdminNotificationItemViewModel
                    {
                        Type =
                            "Arrival",

                        Level =
                            "info",

                        Icon =
                            "fa-door-open",

                        Title =
                            "Khách nhận phòng hôm nay",

                        Description =
                            booking.CustomerName +
                            " • Booking #" +
                            booking.BookingId,

                        TimeText =
                            booking.CheckInDate.ToString(
                                "dd/MM/yyyy"
                            ),

                        BookingId =
                            booking.BookingId
                    }
                );
            }

            var departures =
                db.Bookings
                  .Include(b => b.Room)
                  .Where(b =>
                      b.Status ==
                      "Đang ở" &&
                      b.CheckOutDate >= today &&
                      b.CheckOutDate < tomorrow)
                  .OrderBy(b =>
                      b.CheckOutDate)
                  .ToList();

            foreach (
                var booking
                in departures
            )
            {
                model.Items.Add(
                    new AdminNotificationItemViewModel
                    {
                        Type =
                            "Departure",

                        Level =
                            "purple",

                        Icon =
                            "fa-door-closed",

                        Title =
                            "Khách dự kiến trả phòng hôm nay",

                        Description =
                            booking.CustomerName +
                            " • Booking #" +
                            booking.BookingId,

                        TimeText =
                            booking.CheckOutDate.ToString(
                                "dd/MM/yyyy"
                            ),

                        BookingId =
                            booking.BookingId
                    }
                );
            }

            var currentGuests =
                db.Bookings
                  .Include(b =>
                      b.ThanhToans)
                  .Where(b =>
                      b.Status ==
                      "Đang ở")
                  .ToList();

            foreach (
                var booking
                in currentGuests
            )
            {
                bool hasPaid =
                    booking.ThanhToans.Any(t =>
                        t.TrangThai ==
                        "Đã thanh toán");

                if (!hasPaid)
                {
                    model.Items.Add(
                        new AdminNotificationItemViewModel
                        {
                            Type =
                                "Payment",

                            Level =
                                "danger",

                            Icon =
                                "fa-money-bill-wave",

                            Title =
                                "Khách đang ở chưa thanh toán",

                            Description =
                                booking.CustomerName +
                                " • " +
                                booking.TotalAmount.ToString(
                                    "N0"
                                ) +
                                "đ",

                            TimeText =
                                "Booking #" +
                                booking.BookingId,

                            BookingId =
                                booking.BookingId
                        }
                    );
                }
            }

            model.PendingBookingCount =
                pendingBookings.Count;

            model.ArrivalCount =
                arrivals.Count;

            model.DepartureCount =
                departures.Count;

            model.UnpaidCount =
                currentGuests.Count(b =>
                    HotelOperations.PaymentStatus(b) != "Đã thanh toán");

            return View(model);
        }


        // =========================================================
        // KHÁCH HÀNG NÂNG CAO
        // =========================================================
        public ActionResult Customers(
            string keyword)
        {
            var userQuery =
                db.Users
                  .Where(u =>
                      u.Role ==
                      "Customer");

            if (
                !string.IsNullOrWhiteSpace(
                    keyword
                )
            )
            {
                keyword =
                    keyword.Trim();

                userQuery =
                    userQuery.Where(u =>
                        u.FullName.Contains(
                            keyword
                        ) ||
                        u.Email.Contains(
                            keyword
                        ) ||
                        u.Username.Contains(
                            keyword
                        ));
            }

            var users =
                userQuery
                    .OrderBy(u =>
                        u.FullName)
                    .ToList();

            var result =
                new List<AdminCustomerSummaryViewModel>();

            foreach (
                var user
                in users
            )
            {
                var ownedIds = db.Database.SqlQuery<int>("SELECT BookingId FROM dbo.Bookings WHERE UserId=@p0", user.UserId).ToList();
                var bookings = db.Bookings.Include(b => b.ThanhToans).Where(b => ownedIds.Contains(b.BookingId))
                      .ToList();

                var paidBookings =
                    bookings
                        .Where(b =>
                            b.ThanhToans.Any(t =>
                                t.TrangThai ==
                                "Đã thanh toán"))
                        .ToList();

                result.Add(
                    new AdminCustomerSummaryViewModel
                    {
                        UserId =
                            user.UserId,

                        FullName =
                            user.FullName,

                        Email =
                            user.Email,

                        Username =
                            user.Username,

                        IsActive =
                            user.IsActive,

                        CreatedAt =
                            user.CreatedAt,

                        BookingCount =
                            bookings.Count,

                        CompletedBookingCount =
                            bookings.Count(b =>
                                b.Status ==
                                "Đã trả phòng"),

                        TotalPaid = bookings.SelectMany(b => b.ThanhToans).Where(t => t.TrangThai == "Đã thanh toán").Sum(t => t.SoTien),

                        LastBookingAt =
                            bookings.Count > 0
                            ? (DateTime?)
                              bookings.Max(b =>
                                  b.CreatedAt)
                            : null
                    }
                );
            }

            ViewBag.Keyword =
                keyword;

            ViewBag.CustomerCount =
                result.Count;

            ViewBag.TotalCustomerRevenue =
                result.Sum(x =>
                    x.TotalPaid);

            return View(result);
        }


        public ActionResult CustomerDetails(int id)
        {
            var user =
                db.Users.Find(id);

            if (
                user == null ||
                user.Role !=
                "Customer"
            )
            {
                return HttpNotFound();
            }

            var ownedIds = db.Database.SqlQuery<int>("SELECT BookingId FROM dbo.Bookings WHERE UserId=@p0", user.UserId).ToList();
            var bookings = db.Bookings.Include(b => b.Room).Include(b => b.ThanhToans).Where(b => ownedIds.Contains(b.BookingId))
                  .OrderByDescending(b =>
                      b.CreatedAt)
                  .ToList();

            var model =
                new CustomerDetailsViewModel
                {
                    User =
                        user,

                    Bookings =
                        bookings,

                    TotalPaid = bookings.SelectMany(b => b.ThanhToans).Where(t => t.TrangThai == "Đã thanh toán").Sum(t => t.SoTien)
                };

            return View(model);
        }


        // =========================================================
        // XUẤT CSV
        // =========================================================
        public FileResult ExportBookingsCsv()
        {
            var bookings =
                db.Bookings
                  .Include(b => b.Room)
                  .OrderByDescending(b =>
                      b.CreatedAt)
                  .ToList();

            StringBuilder sb =
                new StringBuilder();

            sb.AppendLine(
                "BookingId,KhachHang,SoDienThoai,Email,Phong,NgayNhan,NgayTra,SoPhong,TongTien,TrangThai,NgayTao"
            );

            foreach (
                var booking
                in bookings
            )
            {
                sb.AppendLine(
                    Csv(booking.BookingId) + "," +
                    Csv(booking.CustomerName) + "," +
                    Csv(booking.CustomerPhone) + "," +
                    Csv(booking.CustomerEmail) + "," +
                    Csv(
                        booking.Room != null
                        ? booking.Room.RoomName
                        : ""
                    ) + "," +
                    Csv(
                        booking.CheckInDate.ToString(
                            "dd/MM/yyyy"
                        )
                    ) + "," +
                    Csv(
                        booking.CheckOutDate.ToString(
                            "dd/MM/yyyy"
                        )
                    ) + "," +
                    Csv(booking.RoomCount) + "," +
                    Csv(booking.TotalAmount) + "," +
                    Csv(booking.Status) + "," +
                    Csv(
                        booking.CreatedAt.ToString(
                            "dd/MM/yyyy HH:mm"
                        )
                    )
                );
            }

            return CsvFile(
                sb.ToString(),
                "bookings_" +
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmm"
                ) +
                ".csv"
            );
        }


        public FileResult ExportPaymentsCsv()
        {
            var bookings =
                db.Bookings
                  .Include(b => b.Room)
                  .Include(b => b.ThanhToans)
                  .ToList();

            StringBuilder sb =
                new StringBuilder();

            sb.AppendLine(
                "BookingId,KhachHang,Phong,SoTien,PhuongThuc,TrangThai,NgayThanhToan,MaGiaoDich,GhiChu"
            );

            foreach (
                var booking
                in bookings
            )
            {
                foreach (
                    var payment
                    in booking.ThanhToans
                )
                {
                    sb.AppendLine(
                        Csv(booking.BookingId) + "," +
                        Csv(booking.CustomerName) + "," +
                        Csv(
                            booking.Room != null
                            ? booking.Room.RoomName
                            : ""
                        ) + "," +
                        Csv(payment.SoTien) + "," +
                        Csv(
                            payment.PhuongThucThanhToan
                        ) + "," +
                        Csv(payment.TrangThai) + "," +
                        Csv(
                            payment.NgayThanhToan.ToString(
                                "dd/MM/yyyy HH:mm"
                            )
                        ) + "," +
                        Csv(payment.MaGiaoDich) + "," +
                        Csv(payment.GhiChu)
                    );
                }
            }

            return CsvFile(
                sb.ToString(),
                "payments_" +
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmm"
                ) +
                ".csv"
            );
        }


        public FileResult ExportCustomersCsv()
        {
            var users =
                db.Users
                  .Where(u =>
                      u.Role ==
                      "Customer")
                  .OrderBy(u =>
                      u.FullName)
                  .ToList();

            StringBuilder sb =
                new StringBuilder();

            sb.AppendLine(
                "UserId,HoTen,Email,Username,TrangThai,SoLanDat,TongDaThanhToan,NgayTao"
            );

            foreach (
                var user
                in users
            )
            {
                var ownedIds = db.Database.SqlQuery<int>("SELECT BookingId FROM dbo.Bookings WHERE UserId=@p0", user.UserId).ToList();
                var bookings = db.Bookings.Include(b => b.ThanhToans).Where(b => ownedIds.Contains(b.BookingId))
                      .ToList();

                decimal totalPaid = bookings.SelectMany(b => b.ThanhToans).Where(t => t.TrangThai == "Đã thanh toán").Sum(t => t.SoTien);

                sb.AppendLine(
                    Csv(user.UserId) + "," +
                    Csv(user.FullName) + "," +
                    Csv(user.Email) + "," +
                    Csv(user.Username) + "," +
                    Csv(
                        user.IsActive
                        ? "Hoạt động"
                        : "Đã khóa"
                    ) + "," +
                    Csv(bookings.Count) + "," +
                    Csv(totalPaid) + "," +
                    Csv(
                        user.CreatedAt.ToString(
                            "dd/MM/yyyy"
                        )
                    )
                );
            }

            return CsvFile(
                sb.ToString(),
                "customers_" +
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmm"
                ) +
                ".csv"
            );
        }


        // =========================================================
        // HELPERS
        // =========================================================
        private List<RoomAvailabilityViewModel>
            BuildRoomAvailability(
                List<Room> rooms,
                List<Booking> bookings, DateTime selectedDate)
        {
            var result =
                new List<RoomAvailabilityViewModel>();

            foreach (
                var room
                in rooms
            )
            {
                var roomBookings =
                    bookings
                        .Where(b =>
                            b.RoomId ==
                            room.RoomId)
                        .ToList();

                int totalPhysical = db.PhysicalRooms.Count(p => p.RoomTypeId == room.RoomId && p.IsActive);
                int availableUnits = HotelOperations.AvailableRooms(db, room.RoomId, selectedDate, selectedDate.AddDays(1)).Count;
                int reservedUnits = totalPhysical - availableUnits;

                int currentGuests =
                    roomBookings.Count(b =>
                        b.Status ==
                        "Đang ở");

                result.Add(
                    new RoomAvailabilityViewModel
                    {
                        RoomId =
                            room.RoomId,

                        RoomName =
                            room.RoomName,

                        RoomType =
                            room.RoomType,

                        Price =
                            room.Price,

                        Capacity =
                            room.Capacity,

                        TotalUnits =
                            totalPhysical,

                        ReservedUnits =
                            reservedUnits,

                        AvailableUnits =
                            availableUnits,

                        CurrentGuestBookings =
                            currentGuests,

                        StatusText =
                            availableUnits > 0
                            ? "Còn phòng"
                            : "Hết phòng",

                        IsAvailable =
                            availableUnits > 0,

                        Bookings =
                            roomBookings
                                .Select(b =>
                                    new RoomBookingMiniViewModel
                                    {
                                        BookingId =
                                            b.BookingId,

                                        CustomerName =
                                            b.CustomerName,

                                        Status =
                                            b.Status,

                                        CheckInDate =
                                            b.CheckInDate,

                                        CheckOutDate =
                                            b.CheckOutDate,

                                        RoomCount =
                                            b.RoomCount
                                    })
                                .OrderBy(x =>
                                    x.CheckInDate)
                                .ToList()
                    }
                );
            }

            return result;
        }


        private string Csv(object value)
        {
            string text =
                value == null
                ? ""
                : Convert.ToString(value);

            if (value is string && text.TrimStart().Length > 0 && "=+-@".IndexOf(text.TrimStart()[0]) >= 0) text = "'" + text;
            text =
                text.Replace(
                    "\"",
                    "\"\""
                );

            return "\"" +
                   text +
                   "\"";
        }


        private FileResult CsvFile(
            string content,
            string fileName)
        {
            byte[] preamble =
                Encoding.UTF8.GetPreamble();

            byte[] body =
                Encoding.UTF8.GetBytes(
                    content
                );

            byte[] result =
                new byte[
                    preamble.Length +
                    body.Length
                ];

            Buffer.BlockCopy(
                preamble,
                0,
                result,
                0,
                preamble.Length
            );

            Buffer.BlockCopy(
                body,
                0,
                result,
                preamble.Length,
                body.Length
            );

            return File(
                result,
                "text/csv",
                fileName
            );
        }


        // =========================================================
        // QUẢN LÝ PHÂN QUYỀN NHÂN VIÊN
        // =========================================================

        [AdminAuthorize]
        [HttpGet]
        public ActionResult StaffPermissions(int? id, string keyword)
        {
            var model = new StaffPermissionsViewModel();
            model.Keyword = keyword;

            var staffQuery = db.Users.Where(u => u.Role == "Staff");
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                staffQuery = staffQuery.Where(u => u.FullName.ToLower().Contains(keyword) || u.Email.ToLower().Contains(keyword) || u.Username.ToLower().Contains(keyword));
            }

            model.StaffList = staffQuery.OrderBy(u => u.FullName).ToList();
            model.AllPermissions = db.Permissions.OrderBy(p => p.Category).ThenBy(p => p.PermissionName).ToList();

            if (id.HasValue)
            {
                model.SelectedStaff = db.Users.FirstOrDefault(u => u.UserId == id.Value && u.Role == "Staff");
                if (model.SelectedStaff != null)
                {
                    model.GrantedPermissionCodes = db.UserPermissions
                        .Where(up => up.UserId == id.Value)
                        .Select(up => up.PermissionCode)
                        .ToList();
                }
            }
            else if (model.StaffList.Any())
            {
                model.SelectedStaff = model.StaffList.First();
                model.GrantedPermissionCodes = db.UserPermissions
                    .Where(up => up.UserId == model.SelectedStaff.UserId)
                    .Select(up => up.PermissionCode)
                    .ToList();
            }

            return View("StaffPermissions", model);
        }

        [AdminAuthorize]
        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        public ActionResult SaveStaffPermissions(int userId, List<string> selectedPermissions)
        {
            var staff = db.Users.FirstOrDefault(u => u.UserId == userId && u.Role == "Staff");
            if (staff == null)
            {
                FailOperation(); TempData["AdminError"] = "Không tìm thấy nhân viên hợp lệ.";
                return RedirectToAction("StaffPermissions");
            }

            // Xóa quyền cũ
            var existingPerms = db.UserPermissions.Where(up => up.UserId == userId).ToList();
            db.UserPermissions.RemoveRange(existingPerms);

            // Thêm quyền mới được chọn
            if (selectedPermissions != null && selectedPermissions.Any())
            {
                foreach (var code in selectedPermissions.Distinct())
                {
                    if (db.Permissions.Any(p => p.PermissionCode == code))
                    {
                        db.UserPermissions.Add(new UserPermission
                        {
                            UserId = userId,
                            PermissionCode = code,
                            GrantedAt = DateTime.Now
                        });
                    }
                }
            }

            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = $"Đã cập nhật phân quyền thành công cho nhân viên {staff.FullName}.";
            return RedirectToAction("StaffPermissions", new { id = userId });
        }


        // Dữ liệu lịch bảo trì được lưu riêng, không ghi đè PhysicalRooms.Status.
        // Bảng dbo.RoomMaintenances phải được tạo bằng script SQL đi kèm trước khi sử dụng.
        public class RoomMaintenanceRecord
        {
            public int MaintenanceId { get; set; }
            public int PhysicalRoomId { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string Reason { get; set; }
            public string Note { get; set; }
            public string Status { get; set; }
            public DateTime? EndedAt { get; set; }
        }

        private List<RoomMaintenanceRecord> GetRoomMaintenanceRecords()
        {
            return db.Database.SqlQuery<RoomMaintenanceRecord>(
                @"SELECT MaintenanceId, PhysicalRoomId, StartDate, EndDate, Reason, Note, Status, EndedAt
                  FROM dbo.RoomMaintenances ORDER BY StartDate DESC, MaintenanceId DESC").ToList();
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        [AdminAuthorize]
        public ActionResult SaveRoomMaintenance(int physicalRoomId, DateTime? startDate,
            DateTime? endDate, string reason, string note)
        {
            try
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    HotelOperations.LockInventory(db);
                    HotelOperations.CreateMaintenance(db, physicalRoomId, startDate, endDate, reason, note);
                    PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
                    transaction.Commit();
                    return Json(new { success = true, message = "Đã lưu lịch bảo trì." });
                }
            }
            catch (HotelRuleException ex) { return Json(new { success = false, message = ex.Message }); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return Json(new { success = false, message = "Không lưu được lịch bảo trì. Vui lòng kiểm tra cấu trúc dữ liệu và thử lại." });
            }
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        [AdminAuthorize]
        public ActionResult EndMaintenanceEarly(int maintenanceId)
        {
            try
            {
                HotelOperations.EndMaintenance(db, maintenanceId);
                PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
                return Json(new { success = true, message = "Đã kết thúc bảo trì; giữ nguyên lịch sử các ngày trước." });
            }
            catch (HotelRuleException ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpGet]
        [AdminAuthorize]
        public JsonResult GetPhysicalRoomsMatrixByDate(string date)
        {
            try
            {
                return Json(RoomMatrixService.Load(db, RoomMatrixService.ParseDate(date)), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Ghi chi tiết ở Output trên localhost để chẩn đoán đúng
                // cột/bảng lỗi, không đưa stack trace cho người dùng.
                System.Diagnostics.Trace.TraceError("GetPhysicalRoomsMatrixByDate: " + ex);
                return Json(new
                {
                    success = false,
                    message = "Không thể tải lịch phòng. Vui lòng liên hệ quản trị viên."
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [QLKhachSan.Filters.HotelAntiForgery]
        [AdminAuthorize]
        public ActionResult ClearAllBookings()
        {
            // Preserve history; release unpaid reservations only, never guests currently staying.
            var pending = db.Bookings.Where(b => (b.Status == "Chờ xác nhận" || b.Status == "Đã xác nhận") &&
                !b.ThanhToans.Any(t => t.TrangThai == "Đã thanh toán")).ToList();
            foreach (var booking in pending)
            {
                HotelOperations.ReturnPromotionUse(db, booking.BookingId);
                booking.Status = "Đã hủy";
                foreach (var payment in db.ThanhToans.Where(t => t.BookingId == booking.BookingId).ToList()) payment.TrangThai = "Đã hủy";
            }
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = "Đã giải phóng " + pending.Count + " đơn chưa thanh toán, chưa nhận phòng. Lịch sử được giữ nguyên.";
            return RedirectToAction("Index");
        }


        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
        // =========================================================
        // CÁC HÀM BỔ SUNG ĐỂ KHẮC PHỤC LỖI THIẾU HÀM ĐỒ ĂN
        // =========================================================
        private decimal GetFoodRevenue(DateTime startDate, DateTime endDate)
        {
            try
            {
                int hasTable = db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DonDatMon'").FirstOrDefault();
                if (hasTable == 0) return 0;

                int hasPaidAt = db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DonDatMon' AND COLUMN_NAME = 'PaidAt'").FirstOrDefault();
                if (hasPaidAt > 0)
                {
                    return db.Database.SqlQuery<decimal>(@"SELECT ISNULL(SUM(TongTien),0) FROM dbo.DonDatMon
                        WHERE TrangThaiThanhToan=N'Đã thanh toán' AND COALESCE(PaidAt,CreatedAt)>=@p0 AND COALESCE(PaidAt,CreatedAt)<@p1", startDate, endDate).FirstOrDefault();
                }
                else
                {
                    return db.Database.SqlQuery<decimal>(@"SELECT ISNULL(SUM(TongTien),0) FROM dbo.DonDatMon
                        WHERE TrangThaiThanhToan=N'Đã thanh toán' AND CreatedAt>=@p0 AND CreatedAt<@p1", startDate, endDate).FirstOrDefault();
                }
            }
            catch
            {
                return 0;
            }
        }

        private int GetPendingFoodOrders()
        {
            try
            {
                int hasTable = db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DonDatMon'").FirstOrDefault();
                if (hasTable == 0) return 0;

                return db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.DonDatMon WHERE TrangThai IN(N'Chờ xác nhận',N'Đã xác nhận',N'Đang chuẩn bị',N'Đang phục vụ',N'Đang giao')").FirstOrDefault();
            }
            catch
            {
                return 0;
            }
        }

        private int GetPaidFoodOrders(DateTime startDate, DateTime endDate)
        {
            try
            {
                int hasTable = db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DonDatMon'").FirstOrDefault();
                if (hasTable == 0) return 0;

                int hasPaidAt = db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DonDatMon' AND COLUMN_NAME = 'PaidAt'").FirstOrDefault();
                if (hasPaidAt > 0)
                {
                    return db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM dbo.DonDatMon WHERE TrangThaiThanhToan=N'Đã thanh toán'
                        AND COALESCE(PaidAt,CreatedAt)>=@p0 AND COALESCE(PaidAt,CreatedAt)<@p1", startDate, endDate).FirstOrDefault();
                }
                else
                {
                    return db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM dbo.DonDatMon WHERE TrangThaiThanhToan=N'Đã thanh toán'
                        AND CreatedAt>=@p0 AND CreatedAt<@p1", startDate, endDate).FirstOrDefault();
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}
