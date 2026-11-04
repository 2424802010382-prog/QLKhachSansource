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
    public class AdminController : Controller
    {
        private readonly HotelDbContext db = new HotelDbContext();

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

            decimal roomToday = db.ThanhToans
                .Where(t => t.TrangThai == "Đã thanh toán" &&
                            t.NgayThanhToan >= today &&
                            t.NgayThanhToan < tomorrow)
                .Select(t => (decimal?)t.SoTien)
                .Sum() ?? 0;

            decimal roomMonth = db.ThanhToans
                .Where(t => t.TrangThai == "Đã thanh toán" &&
                            t.NgayThanhToan >= monthStart &&
                            t.NgayThanhToan < monthEnd)
                .Select(t => (decimal?)t.SoTien)
                .Sum() ?? 0;

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

                decimal rRev = db.ThanhToans
                    .Where(t => t.TrangThai == "Đã thanh toán" &&
                                t.NgayThanhToan >= dStart &&
                                t.NgayThanhToan < dEnd)
                    .Select(t => (decimal?)t.SoTien)
                    .Sum() ?? 0;

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
        [ValidateAntiForgeryToken]
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
                TempData["AdminError"] = "Chỉ có thể xác nhận đơn đang chờ xác nhận.";
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
        [ValidateAntiForgeryToken]
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
                TempData["AdminError"] = "Không thể hủy đơn ở trạng thái hiện tại.";
                return RedirectAfterBookingAction(returnTo);
            }

            bool paid = db.ThanhToans.Any(t =>
                t.BookingId == id && t.TrangThai == "Đã thanh toán");

            if (paid)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Đơn đã thanh toán. Không thể hủy trực tiếp." });
                TempData["AdminError"] = "Đơn đã thanh toán. Không thể hủy trực tiếp.";
                return RedirectAfterBookingAction(returnTo);
            }

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
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmPayment(int id, string returnTo = null)
        {
            var booking = db.Bookings.Find(id);
            if (booking == null)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Không tìm thấy đơn đặt phòng." });
                return HttpNotFound();
            }

            if (booking.Status != "Đã xác nhận" && booking.Status != "Đang ở" && booking.Status != "Chờ xác nhận")
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Trạng thái đơn hiện tại không thể xác nhận thanh toán." });
                TempData["AdminError"] = "Trạng thái đơn hiện tại không thể xác nhận thanh toán.";
                return RedirectAfterBookingAction(returnTo);
            }

            var payment = db.ThanhToans
                .Where(t => t.BookingId == id)
                .OrderByDescending(t => t.NgayThanhToan)
                .FirstOrDefault();

            if (payment != null && payment.TrangThai == "Đã thanh toán")
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Đơn này đã được thanh toán trước đó." });
                TempData["AdminError"] = "Đơn này đã được thanh toán trước đó.";
                return RedirectAfterBookingAction(returnTo);
            }

            if (payment == null)
            {
                payment = new ThanhToan
                {
                    BookingId = booking.BookingId,
                    PhuongThucThanhToan = booking.PaymentMethod,
                    SoTien = booking.TotalAmount,
                    NgayThanhToan = DateTime.Now,
                    TrangThai = "Đã thanh toán",
                    MaGiaoDich = null,
                    GhiChu = "Admin xác nhận thanh toán"
                };
                db.ThanhToans.Add(payment);
            }
            else
            {
                payment.TrangThai = "Đã thanh toán";
                payment.NgayThanhToan = DateTime.Now;
                payment.SoTien = booking.TotalAmount;
                payment.GhiChu = "Admin xác nhận thanh toán";
            }

            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = $"Đã xác nhận thanh toán đơn #{booking.BookingId}.", status = booking.Status });
            }

            TempData["AdminSuccess"] = "Đã xác nhận thanh toán đơn #" + booking.BookingId + ".";
            return RedirectAfterBookingAction(returnTo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckIn(int id, string returnTo = null)
        {
            var booking = db.Bookings.Find(id);
            if (booking == null)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Không tìm thấy đơn đặt phòng." });
                return HttpNotFound();
            }

            if (booking.Status != "Đã xác nhận")
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Chỉ đơn đã xác nhận mới có thể nhận phòng." });
                TempData["AdminError"] = "Chỉ đơn đã xác nhận mới có thể nhận phòng.";
                return RedirectAfterBookingAction(returnTo);
            }

            if (booking.CheckInDate.Date > DateTime.Today)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Chưa đến ngày nhận phòng." });
                TempData["AdminError"] = "Chưa đến ngày nhận phòng.";
                return RedirectAfterBookingAction(returnTo);
            }

            var payment = db.ThanhToans.FirstOrDefault(p => p.BookingId == id);
            if (payment == null || payment.TrangThai != "Đã thanh toán")
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Đơn đặt phòng chưa hoàn tất thanh toán. Không thể nhận phòng." });
                TempData["AdminError"] = "Đơn đặt phòng chưa hoàn tất thanh toán. Không thể nhận phòng.";
                return RedirectAfterBookingAction(returnTo);
            }

            booking.Status = "Đang ở";
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);

            if (Request.IsAjaxRequest())
            {
                return Json(new { success = true, message = $"Đã nhận phòng cho đơn #{booking.BookingId}.", status = booking.Status });
            }

            TempData["AdminSuccess"] = "Đã nhận phòng cho đơn #" + booking.BookingId + ".";
            return RedirectAfterBookingAction(returnTo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                TempData["AdminError"] = "Chỉ khách đang ở mới có thể trả phòng.";
                return RedirectAfterBookingAction(returnTo);
            }

            bool paid = db.ThanhToans.Any(t =>
                t.BookingId == id && t.TrangThai == "Đã thanh toán");

            if (!paid)
            {
                if (Request.IsAjaxRequest()) return Json(new { success = false, message = "Khách chưa thanh toán. Vui lòng xác nhận thanh toán trước." });
                TempData["AdminError"] = "Khách chưa thanh toán. Vui lòng xác nhận thanh toán trước.";
                return RedirectAfterBookingAction(returnTo);
            }

            booking.Status = "Đã trả phòng";
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
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
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
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
            ViewBag.PhysicalRooms = db.PhysicalRooms.Include(pr => pr.RoomType).OrderBy(pr => pr.Floor).ThenBy(pr => pr.RoomNumber).ToList();

            return View(query.OrderBy(r => r.Price).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

            if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(roomType) || price < 0 || capacity <= 0 || totalRooms <= 0)
            {
                TempData["AdminError"] = "Thông tin phòng không hợp lệ.";
                return RedirectToAction("Rooms");
            }

            bool duplicate = id.HasValue
                ? db.Rooms.Any(r => r.RoomName == roomName && r.RoomId != id.Value)
                : db.Rooms.Any(r => r.RoomName == roomName);

            if (duplicate)
            {
                TempData["AdminError"] = "Tên phòng này đã tồn tại.";
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
        [ValidateAntiForgeryToken]
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
                !b.ThanhToans.Any(t => t.TrangThai == "Đã thanh toán"));

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
        [ValidateAntiForgeryToken]
        public ActionResult CreateStaff(string fullName, string email, string password)
        {
            fullName = (fullName ?? "").Trim();
            email = (email ?? "").Trim().ToLowerInvariant();
            password = password ?? "";

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || password.Length < 6)
            {
                TempData["AdminError"] = "Thông tin chưa hợp lệ. Mật khẩu ít nhất 6 ký tự.";
                return RedirectToAction("Users", new { role = "Staff" });
            }

            if (db.Users.Any(u => u.Email == email || u.Username == email))
            {
                TempData["AdminError"] = "Email này đã được sử dụng.";
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
        [ValidateAntiForgeryToken]
        public ActionResult ToggleUser(int id)
        {
            var user = db.Users.Find(id);
            if (user == null) return HttpNotFound();

            if (user.Role == "Admin")
            {
                TempData["AdminError"] = "Không thể khóa tài khoản Admin tại đây.";
                return RedirectToAction("Users");
            }

            user.IsActive = !user.IsActive;
            db.SaveChanges();
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
            TempData["AdminSuccess"] = user.IsActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.";
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeUserRole(int id, string role)
        {
            var user = db.Users.Find(id);
            if (user == null) return HttpNotFound();

            if (user.Role == "Admin")
            {
                TempData["AdminError"] = "Không thể thay đổi quyền Admin.";
                return RedirectToAction("Users");
            }

            if (role != "Customer" && role != "Staff")
            {
                TempData["AdminError"] = "Quyền không hợp lệ.";
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
        [ValidateAntiForgeryToken]
        public ActionResult SaveService(int? id, string tenDichVu, string moTa, decimal gia, string donVi, bool hoatDong = true)
        {
            tenDichVu = (tenDichVu ?? "").Trim();
            donVi = (donVi ?? "").Trim();
            moTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim();

            if (string.IsNullOrWhiteSpace(tenDichVu) || string.IsNullOrWhiteSpace(donVi) || gia < 0)
            {
                TempData["AdminError"] = "Thông tin dịch vụ không hợp lệ.";
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
        [ValidateAntiForgeryToken]
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
        [ValidateAntiForgeryToken]
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

            if (string.IsNullOrWhiteSpace(maCode) || string.IsNullOrWhiteSpace(tenKhuyenMai) || giaTriGiam <= 0 || ngayKetThuc.Date < ngayBatDau.Date)
            {
                TempData["AdminError"] = "Thông tin khuyến mãi không hợp lệ.";
                return RedirectToAction("Promotions");
            }

            if (loaiGiamGia != "PhanTram" && loaiGiamGia != "SoTien")
            {
                TempData["AdminError"] = "Loại giảm giá không hợp lệ.";
                return RedirectToAction("Promotions");
            }

            if (loaiGiamGia == "PhanTram" && giaTriGiam > 100)
            {
                TempData["AdminError"] = "Phần trăm giảm không được lớn hơn 100%.";
                return RedirectToAction("Promotions");
            }

            bool duplicate = id.HasValue
                ? db.KhuyenMais.Any(k => k.MaCode == maCode && k.MaKhuyenMai != id.Value)
                : db.KhuyenMais.Any(k => k.MaCode == maCode);

            if (duplicate)
            {
                TempData["AdminError"] = "Mã khuyến mãi đã tồn tại.";
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
        [ValidateAntiForgeryToken]
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

                decimal roomRevenue = db.ThanhToans
                    .Where(t => t.TrangThai == "Đã thanh toán" && t.NgayThanhToan >= start && t.NgayThanhToan < end)
                    .Select(t => (decimal?)t.SoTien)
                    .Sum() ?? 0;

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
            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
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
                            bookings
                        )
                };

            return View(model);
        }


        // =========================================================
        // CHI TIẾT BOOKING
        // =========================================================
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
                    !b.ThanhToans.Any(t =>
                        t.TrangThai ==
                        "Đã thanh toán"));

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
                var bookings =
                    db.Bookings
                      .Include(b =>
                          b.ThanhToans)
                      .Where(b =>
                          b.CustomerEmail ==
                          user.Email)
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

                        TotalPaid =
                            paidBookings.Sum(b =>
                                b.TotalAmount),

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

            var bookings =
                db.Bookings
                  .Include(b => b.Room)
                  .Include(b => b.ThanhToans)
                  .Where(b =>
                      b.CustomerEmail ==
                      user.Email)
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

                    TotalPaid =
                        bookings
                            .Where(b =>
                                b.ThanhToans.Any(t =>
                                    t.TrangThai ==
                                    "Đã thanh toán"))
                            .Sum(b =>
                                b.TotalAmount)
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
                var bookings =
                    db.Bookings
                      .Include(b =>
                          b.ThanhToans)
                      .Where(b =>
                          b.CustomerEmail ==
                          user.Email)
                      .ToList();

                decimal totalPaid =
                    bookings
                        .Where(b =>
                            b.ThanhToans.Any(t =>
                                t.TrangThai ==
                                "Đã thanh toán"))
                        .Sum(b =>
                            b.TotalAmount);

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
                List<Booking> bookings)
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

                int reservedUnits =
                    roomBookings.Sum(b =>
                        b.RoomCount);

                int availableUnits =
                    Math.Max(
                        0,
                        room.TotalRooms -
                        reservedUnits
                    );

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
                            room.TotalRooms,

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
        [ValidateAntiForgeryToken]
        public ActionResult SaveStaffPermissions(int userId, List<string> selectedPermissions)
        {
            var staff = db.Users.FirstOrDefault(u => u.UserId == userId && u.Role == "Staff");
            if (staff == null)
            {
                TempData["AdminError"] = "Không tìm thấy nhân viên hợp lệ.";
                return RedirectToAction("StaffPermissions");
            }

            // Xóa quyền cũ
            var existingPerms = db.UserPermissions.Where(up => up.UserId == userId).ToList();
            db.UserPermissions.RemoveRange(existingPerms);

            // Thêm quyền mới được chọn
            if (selectedPermissions != null && selectedPermissions.Any())
            {
                foreach (var code in selectedPermissions)
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
        }

        private List<RoomMaintenanceRecord> GetRoomMaintenanceRecords()
        {
            return db.Database.SqlQuery<RoomMaintenanceRecord>(
                @"SELECT MaintenanceId, PhysicalRoomId, StartDate, EndDate, Reason, Note, Status
                  FROM dbo.RoomMaintenances ORDER BY StartDate DESC, MaintenanceId DESC").ToList();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult SaveRoomMaintenance(int physicalRoomId, DateTime? startDate,
            DateTime? endDate, string reason, string note)
        {
            reason = (reason ?? "").Trim();
            note = (note ?? "").Trim();
            if (!startDate.HasValue || !endDate.HasValue ||
                startDate.Value.Date >= endDate.Value.Date || reason.Length == 0 || reason.Length > 500 || note.Length > 2000)
                return Json(new { success = false, message = "Ngày kết thúc phải sau ngày bắt đầu; lý do là bắt buộc (tối đa 500 ký tự)." });

            DateTime start = startDate.Value.Date;
            DateTime end = endDate.Value.Date; // ngày kết thúc không bao gồm
            if (start < DateTime.Today)
                return Json(new { success = false, message = "Không thể tạo lịch bảo trì bắt đầu trong quá khứ." });
            try
            {
                using (var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    var room = db.PhysicalRooms.FirstOrDefault(p => p.PhysicalRoomId == physicalRoomId);
                    if (room == null)
                        return Json(new { success = false, message = "Không tìm thấy phòng vật lý." });

                    string roomNumber = (room.RoomNumber ?? "").Trim();
                    // Kiểm tra đơn trùng ngày của ĐÚNG phòng vật lý, không chặn toàn bộ loại phòng.
                    var candidateBookings = db.Bookings.AsNoTracking()
                        .Where(b => b.RoomId == room.RoomTypeId &&
                            (b.Status == "Chờ xác nhận" || b.Status == "Đã xác nhận" || b.Status == "Đang ở") &&
                            b.CheckInDate < end && b.CheckOutDate > start)
                        .Select(b => new { b.BookingId, b.SpecialRequest }).ToList();
                    var overlapBooking = candidateBookings.FirstOrDefault(b =>
                        PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, roomNumber));
                    if (overlapBooking != null)
                        return Json(new
                        {
                            success = false,
                            message = "Phòng " + roomNumber +
                            " đã có đơn đặt #" + overlapBooking.BookingId + " trong thời gian bảo trì. Không thể lưu."
                        });

                    // UPDLOCK, HOLDLOCK: hai thao tác bảo trì đồng thời không thể cùng giữ một khoảng ngày.
                    int conflicts = db.Database.SqlQuery<int>(
                        @"SELECT COUNT(1) FROM dbo.RoomMaintenances WITH (UPDLOCK, HOLDLOCK)
                          WHERE PhysicalRoomId = @roomId AND Status = N'Đang bảo trì'
                            AND StartDate < @end AND EndDate > @start",
                        new System.Data.SqlClient.SqlParameter("@roomId", physicalRoomId),
                        new System.Data.SqlClient.SqlParameter("@start", start),
                        new System.Data.SqlClient.SqlParameter("@end", end)).Single();
                    if (conflicts > 0)
                        return Json(new { success = false, message = "Phòng đã có lịch bảo trì trùng thời gian." });

                    db.Database.ExecuteSqlCommand(
                        @"INSERT INTO dbo.RoomMaintenances
                          (PhysicalRoomId, StartDate, EndDate, Reason, Note, Status, CreatedAt)
                          VALUES (@roomId, @start, @end, @reason, @note, N'Đang bảo trì', GETDATE())",
                        new System.Data.SqlClient.SqlParameter("@roomId", physicalRoomId),
                        new System.Data.SqlClient.SqlParameter("@start", start),
                        new System.Data.SqlClient.SqlParameter("@end", end),
                        new System.Data.SqlClient.SqlParameter("@reason", reason),
                        new System.Data.SqlClient.SqlParameter("@note", (object)note ?? DBNull.Value));
                    transaction.Commit();
                    return Json(new { success = true, message = "Đã lưu lịch bảo trì phòng " + roomNumber + "." });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("SaveRoomMaintenance: " + ex);
                return Json(new
                {
                    success = false,
                    message = Request.IsLocal
                    ? "Không lưu được lịch bảo trì: " + ex.GetBaseException().Message
                    : "Không lưu được lịch bảo trì. Kiểm tra bảng RoomMaintenances trên database."
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult EndMaintenanceEarly(int maintenanceId)
        {
            try
            {
                // Bảo lưu dữ liệu lịch sử; chỉ đánh dấu kết thúc, không xóa bản ghi.
                int changed = db.Database.ExecuteSqlCommand(
                    @"UPDATE dbo.RoomMaintenances SET Status = N'Đã kết thúc sớm', EndedAt = GETDATE()
                      WHERE MaintenanceId = @id AND Status = N'Đang bảo trì'",
                    new System.Data.SqlClient.SqlParameter("@id", maintenanceId));
                return Json(changed > 0
                    ? new { success = true, message = "Đã kết thúc lịch bảo trì." }
                    : new { success = false, message = "Lịch bảo trì không tồn tại hoặc đã kết thúc." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("EndMaintenanceEarly: " + ex);
                return Json(new { success = false, message = "Không thể kết thúc bảo trì. Kiểm tra bảng dữ liệu." });
            }
        }

        [HttpGet]
        [AdminAuthorize]
        public JsonResult GetPhysicalRoomsMatrixByDate(string date)
        {
            try
            {
                DateTime targetDate;
                if (string.IsNullOrWhiteSpace(date) ||
                    !DateTime.TryParseExact(date, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out targetDate))
                {
                    targetDate = DateTime.Today;
                }

                var dateOnly = targetDate.Date;
                var nextDate = dateOnly.AddDays(1);

                // Dùng phép so sánh theo khoảng ngày để không phát sinh SQL
                // CAST/TruncateTime và không kéo theo bảng Room qua Include.
                var physicalRooms = db.PhysicalRooms.AsNoTracking()
                    .Include(pr => pr.RoomType)
                    .OrderBy(pr => pr.Floor)
                    .ThenBy(pr => pr.RoomNumber)
                    .ToList();

                // Chỉ SELECT những cột thật sự dùng trong ma trận.
                // Không Include(b => b.Room): quan hệ này có thể làm truy vấn
                // thất bại nếu mapping của Room trên DB đã thay đổi.
                var bookings = db.Bookings.AsNoTracking()
                    .Where(b => (b.Status == "Chờ xác nhận" ||
                                 b.Status == "Đã xác nhận" ||
                                 b.Status == "Đang ở") &&
                                b.CheckOutDate > dateOnly)
                    .Select(b => new
                    {
                        b.BookingId,
                        b.RoomId,
                        b.SpecialRequest,
                        b.CheckInDate,
                        b.CheckOutDate,
                        b.Status,
                        b.CustomerName,
                        b.CustomerPhone,
                        b.CustomerEmail,
                        b.TotalAmount
                    })
                    .OrderBy(b => b.CheckInDate)
                    .ToList();

                var activeBookings = bookings
                    .Where(b => b.CheckInDate < nextDate && b.CheckOutDate > dateOnly)
                    .ToList();

                // Chỉ một truy vấn thanh toán thay vì truy vấn cho từng phòng.
                var relevantBookingIds = activeBookings.Select(b => b.BookingId).ToList();
                var paidBookingIds = new HashSet<int>(
                    db.ThanhToans.AsNoTracking()
                        .Where(t => relevantBookingIds.Contains(t.BookingId) &&
                                    t.TrangThai == "Đã thanh toán")
                        .Select(t => t.BookingId)
                        .ToList());

                var maintenanceRecords = GetRoomMaintenanceRecords();
                var roomListResult = new List<object>();
                foreach (var pr in physicalRooms)
                {
                    string roomNum = (pr.RoomNumber ?? "").Trim();
                    string statusOnDate = "Trống";
                    var roomMaintenanceHistory = maintenanceRecords
                        .Where(m => m.PhysicalRoomId == pr.PhysicalRoomId).ToList();
                    var activeMaintenance = roomMaintenanceHistory.FirstOrDefault(m =>
                        m.Status == "Đang bảo trì" && m.StartDate < nextDate && m.EndDate > dateOnly);
                    bool isMaintenance = pr.Status == "Bảo trì" || activeMaintenance != null;
                    if (isMaintenance)
                    {
                        statusOnDate = "Bảo trì";
                    }

                    var matchedBooking = activeBookings.FirstOrDefault(b =>
                        b.RoomId == pr.RoomTypeId &&
                        !string.IsNullOrWhiteSpace(b.SpecialRequest) &&
                        PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, roomNum));

                    object bookingDetail = null;
                    if (matchedBooking != null && !isMaintenance)
                    {
                        if (matchedBooking.Status == "Đang ở") statusOnDate = "Đang ở";
                        else if (matchedBooking.Status == "Đã xác nhận") statusOnDate = "Đã đặt";
                        else statusOnDate = "Chờ xác nhận";

                        bookingDetail = new
                        {
                            bookingId = matchedBooking.BookingId,
                            customerName = string.IsNullOrWhiteSpace(matchedBooking.CustomerName) ? "Khách hàng" : matchedBooking.CustomerName,
                            customerPhone = string.IsNullOrWhiteSpace(matchedBooking.CustomerPhone) ? "Chưa cung cấp" : matchedBooking.CustomerPhone,
                            customerEmail = string.IsNullOrWhiteSpace(matchedBooking.CustomerEmail) ? "Chưa cung cấp" : matchedBooking.CustomerEmail,
                            checkInDate = matchedBooking.CheckInDate.ToString("dd/MM/yyyy"),
                            checkOutDate = matchedBooking.CheckOutDate.ToString("dd/MM/yyyy"),
                            status = matchedBooking.Status,
                            totalAmount = matchedBooking.TotalAmount.ToString("N0") + "đ",
                            isPaid = paidBookingIds.Contains(matchedBooking.BookingId)
                        };
                    }

                    var upcoming = bookings
                        .Where(b => b.RoomId == pr.RoomTypeId &&
                                    (matchedBooking == null || b.BookingId != matchedBooking.BookingId) &&
                                    !string.IsNullOrWhiteSpace(b.SpecialRequest) &&
                                    PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, roomNum))
                        .Take(5)
                        .Select(b => new
                        {
                            bookingId = b.BookingId,
                            customerName = string.IsNullOrWhiteSpace(b.CustomerName) ? "Khách hàng" : b.CustomerName,
                            checkInDate = b.CheckInDate.ToString("dd/MM/yyyy"),
                            checkOutDate = b.CheckOutDate.ToString("dd/MM/yyyy"),
                            status = b.Status
                        }).ToList();

                    roomListResult.Add(new
                    {
                        physicalRoomId = pr.PhysicalRoomId,
                        roomNumber = pr.RoomNumber,
                        floor = pr.Floor,
                        roomTypeName = pr.RoomType != null ? pr.RoomType.RoomName : "Phòng",
                        status = statusOnDate,
                        currentBooking = bookingDetail,
                        currentMaintenance = activeMaintenance == null ? null : new
                        {
                            maintenanceId = activeMaintenance.MaintenanceId,
                            reason = activeMaintenance.Reason,
                            note = activeMaintenance.Note,
                            status = activeMaintenance.Status,
                            startDate = activeMaintenance.StartDate.ToString("dd/MM/yyyy"),
                            endDate = activeMaintenance.EndDate.ToString("dd/MM/yyyy")
                        },
                        maintenanceHistory = roomMaintenanceHistory.Select(m => new
                        {
                            maintenanceId = m.MaintenanceId,
                            reason = m.Reason,
                            status = m.Status,
                            startDate = m.StartDate.ToString("dd/MM/yyyy"),
                            endDate = m.EndDate.ToString("dd/MM/yyyy")
                        }).ToList(),
                        upcomingBookings = upcoming
                    });
                }

                return Json(new
                {
                    success = true,
                    selectedDate = dateOnly.ToString("yyyy-MM-dd"),
                    displayDate = dateOnly.ToString("dd/MM/yyyy"),
                    rooms = roomListResult
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Ghi chi tiết ở Output trên localhost để chẩn đoán đúng
                // cột/bảng lỗi, không đưa stack trace cho người dùng.
                System.Diagnostics.Trace.TraceError("GetPhysicalRoomsMatrixByDate: " + ex);
                var sqlError = ex;
                while (sqlError.InnerException != null)
                    sqlError = sqlError.InnerException;
                return Json(new
                {
                    success = false,
                    message = Request.IsLocal ? ("Không thể tải lịch phòng: " + sqlError.Message) : "Không thể tải lịch phòng. Vui lòng liên hệ quản trị viên."
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public ActionResult ClearAllBookings()
        {
            try
            {
                db.Database.ExecuteSqlCommand("DELETE FROM DatPhongDichVu");
                db.Database.ExecuteSqlCommand("DELETE FROM ThanhToan");
                db.Database.ExecuteSqlCommand("DELETE FROM DanhGia");
                db.Database.ExecuteSqlCommand("DELETE FROM Bookings");
                db.Database.ExecuteSqlCommand("UPDATE PhysicalRooms SET Status = 'Trống'");

                PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
                TempData["AdminSuccess"] = "Đã giải phóng toàn bộ dữ liệu đặt phòng và làm mới 40 phòng vật lý thành công!";
            }
            catch (Exception ex)
            {
                TempData["AdminError"] = "Lỗi khi giải phóng dữ liệu: " + ex.Message;
            }

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
            // Nếu dự án chưa có module/bảng đồ ăn, tạm thời trả về 0 để hết lỗi biên dịch
            return 0;
        }

        private int GetPendingFoodOrders()
        {
            // Tạm thời trả về 0 nếu chưa có bảng quản lý đơn đồ ăn
            return 0;
        }

        private int GetPaidFoodOrders(DateTime startDate, DateTime endDate)
        {
            // Tạm thời trả về 0 nếu chưa có bảng quản lý đơn đồ ăn
            return 0;
        }
    }
}
