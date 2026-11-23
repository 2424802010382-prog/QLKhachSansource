using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;

namespace QLKhachSan.Models
{
    public sealed class HotelRuleException : InvalidOperationException
    {
        public HotelRuleException(string message) : base(message) { }
    }

    // Existing [Phòng: 101] assignments remain readable; user notes never create assignments.
    public static class HotelOperations
    {
        public static readonly string[] ActiveStatuses = { "Chờ xác nhận", "Đã xác nhận", "Đang ở" };
        public static DateTime VietnamToday()
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
            }
            catch
            {
                try
                {
                    var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
                }
                catch
                {
                    return DateTime.UtcNow.AddHours(7).Date;
                }
            }
        }
        public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd)
        { return start < otherEnd && end > otherStart; }

        public static List<string> AssignedNumbers(string notes)
        {
            return Regex.Matches(notes ?? "", @"\[Phòng:\s*([^\]]+)\]", RegexOptions.IgnoreCase)
                .Cast<Match>().Select(m => m.Groups[1].Value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string AssignmentNotes(IEnumerable<PhysicalRoom> rooms, string notes)
        {
            var clean = Regex.Replace(notes ?? "", @"\[Phòng:[^\]]*\]", "", RegexOptions.IgnoreCase).Trim();
            return string.Join(" ", rooms.Select(p => "[Phòng: " + p.RoomNumber.Trim() + "]")) + " " + clean;
        }

        public static bool ManualMaintenance(PhysicalRoom room)
        { return room.Status == "Bảo trì thủ công"; }

        public static bool ManualMaintenance(PhysicalRoom room, IEnumerable<RoomMaintenance> history)
        {
            // Preserve legacy manual flags when there is no scheduled history at all.
            return ManualMaintenance(room) || (room.Status == "Bảo trì" && !history.Any(m => m.PhysicalRoomId == room.PhysicalRoomId));
        }

        public static DateTime MaintenanceEnd(RoomMaintenance maintenance)
        {
            return maintenance.EndedAt.HasValue && maintenance.EndedAt.Value < maintenance.EndDate
                ? maintenance.EndedAt.Value : maintenance.EndDate;
        }

        public static bool MaintenanceOverlaps(RoomMaintenance m, DateTime start, DateTime end)
        {
            return MaintenanceEnd(m) > m.StartDate && (m.Status == "Đang bảo trì" || m.Status == "Đã kết thúc sớm") &&
                Overlaps(start, end, m.StartDate, MaintenanceEnd(m));
        }

        // Shared transaction lock for bookings, check-in/out, payment and maintenance.
        public static void LockInventory(HotelDbContext db)
        {
            if (db.Database.CurrentTransaction == null)
                throw new HotelRuleException("Thao tác phòng cần transaction.");
            int result = db.Database.SqlQuery<int>(@"DECLARE @r int;
                EXEC @r = sys.sp_getapplock @Resource=N'LuxuryHotel.Inventory',
                    @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
                SELECT @r;").Single();
            if (result < 0) throw new HotelRuleException("Hệ thống đang xử lý phòng. Vui lòng thử lại.");
        }

        public static List<PhysicalRoom> AvailableRooms(HotelDbContext db, int roomTypeId,
            DateTime start, DateTime end, int? excludedBookingId = null)
        {
            if (end <= start) return new List<PhysicalRoom>();
            var rooms = db.PhysicalRooms.AsNoTracking().Include(p => p.Room)
                .Where(p => p.RoomTypeId == roomTypeId && p.IsActive && p.Room.IsActive)
                .OrderBy(p => p.RoomNumber).ToList();
            var bookings = db.Bookings.AsNoTracking().Where(b => b.RoomId == roomTypeId &&
                ActiveStatuses.Contains(b.Status) && b.CheckInDate < end && b.CheckOutDate > start &&
                (!excludedBookingId.HasValue || b.BookingId != excludedBookingId.Value)).ToList();
            var maintenances = db.RoomMaintenances.AsNoTracking()
                .Where(m => m.PhysicalRoom.RoomTypeId == roomTypeId).ToList();
            return AvailableRooms(rooms, bookings, maintenances, start, end);
        }

        public static List<PhysicalRoom> AvailableRooms(List<PhysicalRoom> rooms, List<Booking> bookings,
            List<RoomMaintenance> maintenances, DateTime start, DateTime end)
        {
            var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int unassigned = 0;
            foreach (var booking in bookings.Where(b => ActiveStatuses.Contains(b.Status) &&
                Overlaps(start, end, b.CheckInDate, b.CheckOutDate)))
            {
                var numbers = AssignedNumbers(booking.SpecialRequest)
                    .Where(n => rooms.Any(p => p.RoomTypeId == booking.RoomId &&
                        string.Equals(p.RoomNumber.Trim(), n, StringComparison.OrdinalIgnoreCase))).ToList();
                foreach (string n in numbers) occupied.Add(n);
                unassigned += Math.Max(0, booking.RoomCount - numbers.Count);
            }
            // Old bookings without tags reserve capacity conservatively, without editing history.
            return rooms.Where(p => p.IsActive && !ManualMaintenance(p, maintenances) && !occupied.Contains(p.RoomNumber.Trim()) &&
                    !maintenances.Any(m => m.PhysicalRoomId == p.PhysicalRoomId && MaintenanceOverlaps(m, start, end)))
                .OrderBy(p => p.RoomNumber).Skip(unassigned).ToList();
        }

        public static List<PhysicalRoom> SelectRooms(HotelDbContext db, int roomTypeId,
            DateTime start, DateTime end, int count, IEnumerable<string> numbers, bool requireSelection)
        {
            var requested = (numbers ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim()).ToList();
            if (count < 1 || requested.Distinct(StringComparer.OrdinalIgnoreCase).Count() != requested.Count ||
                (requireSelection && requested.Count != count) || requested.Count > count)
                throw new HotelRuleException("Vui lòng chọn đúng số lượng phòng khác nhau.");
            var available = AvailableRooms(db, roomTypeId, start, end);
            if (requested.Any(n => !available.Any(p => string.Equals(p.RoomNumber.Trim(), n, StringComparison.OrdinalIgnoreCase))))
                throw new HotelRuleException("Phòng đã chọn không thuộc loại phòng, đã được đặt hoặc đang bảo trì.");
            var selected = available.Where(p => requested.Contains(p.RoomNumber.Trim(), StringComparer.OrdinalIgnoreCase)).ToList();
            selected.AddRange(available.Where(p => !selected.Any(s => s.PhysicalRoomId == p.PhysicalRoomId)).Take(count - selected.Count));
            if (selected.Count != count) throw new HotelRuleException("Không đủ phòng vật lý trống trong khoảng ngày này.");
            return selected;
        }

        public static string RoomStatus(PhysicalRoom room, IEnumerable<Booking> bookings,
            IEnumerable<RoomMaintenance> maintenances, DateTime date)
        {
            DateTime today = VietnamToday();
            DateTime start = date.Date, end = start.AddDays(1);
            if (!room.IsActive || (room.Room != null && !room.Room.IsActive)) return "Ngừng hoạt động";
            if (ManualMaintenance(room, maintenances) || maintenances.Any(m => m.PhysicalRoomId == room.PhysicalRoomId && MaintenanceOverlaps(m, start, end)))
                return "Bảo trì";
            var booking = bookings.Where(b => b.RoomId == room.RoomTypeId && ActiveStatuses.Contains(b.Status) &&
                    Overlaps(start, end, b.CheckInDate, b.CheckOutDate) &&
                    PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, room.RoomNumber.Trim()))
                .OrderBy(b => b.Status == "Đang ở" ? 0 : b.Status == "Đã xác nhận" ? 1 : 2)
                .ThenBy(b => b.BookingId).FirstOrDefault();
            if (booking == null)
            {
                if (start < today && bookings.Any(b => b.RoomId == room.RoomTypeId && b.Status == "Đã trả phòng" &&
                    Overlaps(start, end, b.CheckInDate, b.CheckOutDate) && PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, room.RoomNumber.Trim())))
                    return "Đã trả phòng";
                return "Trống";
            }
            return booking.Status == "Đã xác nhận" ? "Đã đặt" : booking.Status;
        }

        public static List<PhysicalRoom> RoomSnapshot(HotelDbContext db, DateTime date)
        {
            DateTime today = VietnamToday();
            var rooms = db.PhysicalRooms.AsNoTracking().Include(p => p.Room).OrderBy(p => p.Floor).ThenBy(p => p.RoomNumber).ToList();
            DateTime start = date.Date, end = start.AddDays(1);
            var bookings = db.Bookings.AsNoTracking().Where(b => (ActiveStatuses.Contains(b.Status) ||
                (start < today && b.Status == "Đã trả phòng")) && b.CheckInDate < end && b.CheckOutDate > start).ToList();
            var maintenance = db.RoomMaintenances.AsNoTracking().ToList();
            foreach (var room in rooms) room.Status = RoomStatus(room, bookings, maintenance, date);
            return rooms;
        }

        public static void ValidateCheckIn(HotelDbContext db, Booking booking)
        {
            DateTime today = VietnamToday();
            if (booking.Status != "Đã xác nhận" && booking.Status != "Đang ở")
                throw new HotelRuleException("Đơn phải ở trạng thái đã xác nhận.");
            if (booking.CheckOutDate.Date <= today)
                throw new HotelRuleException("Đơn đã quá ngày lưu trú.");
            var paid = db.ThanhToans.Where(p => p.BookingId == booking.BookingId && p.TrangThai == "Đã thanh toán")
                .Select(p => (decimal?)p.SoTien).Sum() ?? 0;
            if (paid < booking.TotalAmount) throw new HotelRuleException("Đơn chưa thanh toán đủ.");
            EnsureReservation(db, booking);
        }

        public static void EnsureReservation(HotelDbContext db, Booking booking)
        {
            DateTime today = VietnamToday();
            if (booking.CheckOutDate.Date < today || booking.CheckOutDate <= booking.CheckInDate || booking.RoomCount < 1)
                throw new HotelRuleException("Đơn đã quá ngày lưu trú hoặc có khoảng ngày không hợp lệ.");
            var numbers = AssignedNumbers(booking.SpecialRequest);
            var available = AvailableRooms(db, booking.RoomId, booking.CheckInDate, booking.CheckOutDate, booking.BookingId);
            if (numbers.Count < booking.RoomCount)
            {
                var assigned = available.Where(p => numbers.Contains(p.RoomNumber.Trim(), StringComparer.OrdinalIgnoreCase)).ToList();
                assigned.AddRange(available.Where(p => !assigned.Any(a => a.PhysicalRoomId == p.PhysicalRoomId)).Take(booking.RoomCount - assigned.Count));
                if (assigned.Count == 0)
                {
                    assigned = db.PhysicalRooms.Where(p => p.RoomTypeId == booking.RoomId && p.IsActive).Take(booking.RoomCount).ToList();
                }
                if (assigned.Count == 0) throw new HotelRuleException("Không tìm thấy phòng vật lý khả dụng cho loại phòng này.");
                booking.SpecialRequest = AssignmentNotes(assigned, booking.SpecialRequest);
            }
        }

        public static void ConfirmBookingPayment(HotelDbContext db, Booking booking, string evidence, int actorId)
        {
            evidence = (evidence ?? "").Trim();
            if (evidence.Length < 3 || evidence.Length > 200)
                throw new HotelRuleException("Cần nhập chứng từ thu tiền hoặc mã giao dịch đã đối soát (3–200 ký tự).");
            if (!ActiveStatuses.Contains(booking.Status))
                throw new HotelRuleException("Trạng thái đơn không thể nhận thanh toán.");
            var payments = db.ThanhToans.Where(t => t.BookingId == booking.BookingId).ToList();
            decimal paid = payments.Where(p => p.TrangThai == "Đã thanh toán").Sum(p => p.SoTien);
            if (paid >= booking.TotalAmount)
                throw new HotelRuleException("Đơn đã thanh toán đủ trước đó.");
            bool bank = booking.PaymentMethod == "Chuyển khoản";
            if (bank && (SePayReferenceUsed(db, evidence) || RoomServiceReferenceUsed(db, evidence))) throw new HotelRuleException("Giao dịch đã được SePay ghi nhận, không thể thu trùng.");
            if (bank && db.ThanhToans.Any(t => t.MaGiaoDich == evidence && t.TrangThai == "Đã thanh toán"))
                throw new HotelRuleException("Mã giao dịch đã được ghi nhận. Không thể thu trùng.");
            if (bank && db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.DonDatMon WHERE PaymentReference=@p0 AND TrangThaiThanhToan=N'Đã thanh toán'", evidence).Single() > 0)
                throw new HotelRuleException("Mã giao dịch đã được dùng cho đơn món.");
            var payment = payments.Where(p => p.TrangThai != "Đã thanh toán" && p.TrangThai != "Đã hủy")
                .OrderByDescending(p => p.MaThanhToan).FirstOrDefault();
            if (payment == null)
            {
                payment = new ThanhToan { BookingId = booking.BookingId };
                db.ThanhToans.Add(payment);
            }
            payment.PhuongThucThanhToan = booking.PaymentMethod;
            payment.SoTien = booking.TotalAmount - paid;
            payment.TrangThai = "Đã thanh toán";
            payment.NgayThanhToan = DateTime.Now;
            payment.MaGiaoDich = bank ? evidence : null;
            payment.GhiChu = "Xác minh thủ công bởi tài khoản #" + actorId + ": " + evidence;
            db.SaveChanges();
            db.Database.ExecuteSqlCommand(@"UPDATE dbo.DonDatMon SET TrangThaiThanhToan=N'Đã thanh toán',
                PaidAt=@p0, PaymentEvidence=@p1 WHERE BookingId=@p2 AND PayWithBooking=1
                AND TrangThai<>N'Đã hủy' AND TrangThaiThanhToan<>N'Đã thanh toán'",
                payment.NgayThanhToan, "Thu cùng hóa đơn phòng bởi #" + actorId + ": " + evidence, booking.BookingId);
        }

        public static decimal PaidAmount(Booking booking)
        { return (booking.ThanhToans ?? new List<ThanhToan>()).Where(p => p.TrangThai == "Đã thanh toán").Sum(p => p.SoTien); }

        public static string PaymentStatus(Booking booking)
        {
            decimal paid = PaidAmount(booking);
            if (paid >= booking.TotalAmount) return "Đã thanh toán";
            if (booking.Status == "Đã hủy") return "Đã hủy";
            var pending = (booking.ThanhToans ?? new List<ThanhToan>()).Where(p => p.TrangThai != "Đã thanh toán" && p.TrangThai != "Đã hủy")
                .OrderByDescending(p => p.MaThanhToan).FirstOrDefault();
            return pending != null ? pending.TrangThai : paid > 0 ? "Chưa thanh toán đủ" : "Chờ thanh toán";
        }

        public static ThanhToan SynchronizeOutstandingPayment(HotelDbContext db, Booking booking)
        {
            var rows = db.ThanhToans.Where(p => p.BookingId == booking.BookingId).ToList();
            decimal due = Math.Max(0, booking.TotalAmount - rows.Where(p => p.TrangThai == "Đã thanh toán").Sum(p => p.SoTien));
            var pending = rows.Where(p => p.TrangThai != "Đã thanh toán" && p.TrangThai != "Đã hủy").OrderByDescending(p => p.MaThanhToan).ToList();
            if (due == 0)
            {
                foreach (var row in pending) row.TrangThai = "Đã hủy";
                db.SaveChanges(); return null;
            }
            var payment = pending.FirstOrDefault();
            if (payment == null)
            {
                payment = new ThanhToan { BookingId = booking.BookingId, NgayThanhToan = DateTime.Now, TrangThai = "Chờ thanh toán",
                    GhiChu = "Khoản còn phải thu; chưa ghi nhận đã nhận tiền." };
                db.ThanhToans.Add(payment);
            }
            foreach (var old in pending.Skip(1)) old.TrangThai = "Đã hủy";
            payment.SoTien = due; payment.PhuongThucThanhToan = booking.PaymentMethod;
            db.SaveChanges(); return payment;
        }

        private static bool? _hasDonDatMonCols;
        public static decimal RoomRevenue(HotelDbContext db, DateTime start, DateTime end)
        {
            try
            {
                if (!_hasDonDatMonCols.HasValue)
                {
                    int hasCols = db.Database.SqlQuery<int>(@"
                        SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'DonDatMon' AND COLUMN_NAME IN ('PayWithBooking', 'PaidAt', 'TrangThaiThanhToan')
                    ").FirstOrDefault();
                    _hasDonDatMonCols = (hasCols >= 3);
                }

                if (_hasDonDatMonCols.Value)
                {
                    return db.Database.SqlQuery<decimal>(@"SELECT
                        ISNULL((SELECT SUM(SoTien) FROM dbo.ThanhToan WHERE TrangThai=N'Đã thanh toán' AND NgayThanhToan>=@p0 AND NgayThanhToan<@p1),0)
                        - ISNULL((SELECT SUM(TongTien) FROM dbo.DonDatMon WHERE PayWithBooking=1 AND TrangThaiThanhToan=N'Đã thanh toán'
                            AND PaidAt>=@p0 AND PaidAt<@p1),0)", start, end).FirstOrDefault();
                }
            }
            catch { }

            try
            {
                return db.Database.SqlQuery<decimal>(@"SELECT
                    ISNULL((SELECT SUM(SoTien) FROM dbo.ThanhToan WHERE TrangThai=N'Đã thanh toán' AND NgayThanhToan>=@p0 AND NgayThanhToan<@p1),0)", start, end).FirstOrDefault();
            }
            catch
            {
                return 0;
            }
        }

        public static bool CanChangeFoodStatus(string current, string next)
        {
            if (current == "Chờ xác nhận") return next == "Đã xác nhận" || next == "Đã hủy";
            if (current == "Đã xác nhận") return next == "Đang chuẩn bị" || next == "Đã hủy";
            if (current == "Đang chuẩn bị") return next == "Đang phục vụ" || next == "Đang giao" || next == "Đã hủy";
            return (current == "Đang phục vụ" || current == "Đang giao") && next == "Hoàn tất";
        }

        public sealed class FoodOrderState
        {
            public int MaDon { get; set; }
            public int? UserId { get; set; }
            public int? BookingId { get; set; }
            public int? BookingServiceId { get; set; }
            public bool PayWithBooking { get; set; }
            public string TrangThai { get; set; }
            public string TrangThaiThanhToan { get; set; }
            public string PhuongThucThanhToan { get; set; }
            public decimal TongTien { get; set; }
        }

        public static FoodOrderState FoodOrder(HotelDbContext db, int id)
        {
            return db.Database.SqlQuery<FoodOrderState>(@"SELECT MaDon,UserId,BookingId,BookingServiceId,PayWithBooking,
                TrangThai,TrangThaiThanhToan,PhuongThucThanhToan,TongTien FROM dbo.DonDatMon WHERE MaDon=@p0", id).SingleOrDefault();
        }

        public static void ChangeFoodStatus(HotelDbContext db, int id, string status)
        {
            var order = FoodOrder(db, id);
            if (order == null || !CanChangeFoodStatus(order.TrangThai, status))
                throw new HotelRuleException("Không thể chuyển trạng thái đơn món.");
            if (status == "Đã hủy" && order.TrangThaiThanhToan == "Đã thanh toán")
                throw new HotelRuleException("Đơn đã thu tiền cần quy trình hoàn tiền, không thể hủy trực tiếp.");
            if (status == "Đã hủy" && order.PayWithBooking && order.BookingId.HasValue)
            {
                var booking = db.Bookings.Find(order.BookingId.Value);
                if (booking == null || booking.TotalAmount < order.TongTien) throw new HotelRuleException("Hóa đơn phòng không khớp.");
                booking.TotalAmount -= order.TongTien;
                if (order.BookingServiceId.HasValue)
                {
                    var service = db.DatPhongDichVus.Find(order.BookingServiceId.Value);
                    if (service != null) { service.SoLuong = 0; service.ThanhTien = 0; }
                }
                db.SaveChanges();
                SynchronizeOutstandingPayment(db, booking);
            }
            db.Database.ExecuteSqlCommand("UPDATE dbo.DonDatMon SET TrangThai=@p0 WHERE MaDon=@p1", status, id);
        }

        public static void ConfirmFoodPayment(HotelDbContext db, int id, string evidence, int actorId)
        {
            var order = FoodOrder(db, id);
            evidence = (evidence ?? "").Trim();
            if (order == null || order.TrangThai == "Đã hủy" || order.TrangThaiThanhToan == "Đã thanh toán")
                throw new HotelRuleException("Đơn không thể ghi nhận thanh toán hoặc đã thanh toán.");
            if (order.PayWithBooking) throw new HotelRuleException("Đơn này được thanh toán cùng hóa đơn phòng.");
            if (evidence.Length < 3 || evidence.Length > 200) throw new HotelRuleException("Cần chứng từ thu tiền hoặc mã giao dịch đã đối soát (3–200 ký tự).");
            bool bank = order.PhuongThucThanhToan == "Chuyển khoản";
            if (bank && (SePayReferenceUsed(db, evidence) || RoomServiceReferenceUsed(db, evidence))) throw new HotelRuleException("Giao dịch đã được SePay ghi nhận, không thể thu trùng.");
            if (bank && (db.ThanhToans.Any(p => p.MaGiaoDich == evidence && p.TrangThai == "Đã thanh toán") ||
                db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.DonDatMon WHERE PaymentReference=@p0", evidence).Single() > 0))
                throw new HotelRuleException("Mã giao dịch đã được ghi nhận.");
            db.Database.ExecuteSqlCommand(@"UPDATE dbo.DonDatMon SET TrangThaiThanhToan=N'Đã thanh toán', PaidAt=@p0,
                PaymentReference=@p1, PaymentEvidence=@p2 WHERE MaDon=@p3 AND TrangThaiThanhToan<>N'Đã thanh toán'",
                DateTime.Now, bank ? (object)evidence : DBNull.Value, "Xác minh thủ công bởi #" + actorId + ": " + evidence, id);
        }

        public static bool RoomServiceReferenceUsed(HotelDbContext db, string reference, string alternate = null)
        {
            return db.Database.SqlQuery<int>(@"IF OBJECT_ID('dbo.RoomServiceOrders','U') IS NULL SELECT 0
                ELSE SELECT COUNT(*) FROM dbo.RoomServiceOrders WHERE Evidence=@p0 OR Evidence=@p1", reference, (object)alternate ?? DBNull.Value).Single() > 0;
        }
        private static bool SePayReferenceUsed(HotelDbContext db, string reference)
        {
            return db.Database.SqlQuery<int>(@"IF OBJECT_ID('dbo.SePayTransactions','U') IS NULL SELECT 0
                ELSE SELECT COUNT(*) FROM dbo.SePayTransactions WHERE Status='Applied'
                AND (BankReference=@p0 OR 'SEPAY:'+CONVERT(varchar(30),SePayId)=@p0)", reference).Single() > 0;
        }

        public static void ReturnPromotionUse(HotelDbContext db, int bookingId)
        {
            db.Database.ExecuteSqlCommand(@"UPDATE k SET SoLuotDaSuDung = SoLuotDaSuDung - 1
                FROM dbo.KhuyenMai k INNER JOIN dbo.Bookings b ON b.MaKhuyenMai=k.MaKhuyenMai
                WHERE b.BookingId=@p0 AND k.SoLuotDaSuDung>0", bookingId);
        }

        public static RoomMaintenance CreateMaintenance(HotelDbContext db, int physicalRoomId,
            DateTime? startDate, DateTime? endDate, string reason, string note)
        {
            DateTime today = VietnamToday();
            reason = (reason ?? "").Trim(); note = (note ?? "").Trim();
            if (!startDate.HasValue || !endDate.HasValue || startDate.Value.Date < today ||
                endDate.Value.Date <= startDate.Value.Date || reason.Length == 0 || reason.Length > 500 || note.Length > 2000)
                throw new HotelRuleException("Ngày bảo trì phải hợp lệ, không bắt đầu trong quá khứ; lý do bắt buộc (tối đa 500 ký tự).");
            var room = db.PhysicalRooms.Find(physicalRoomId);
            if (room == null || !room.IsActive || !AvailableRooms(db, room.RoomTypeId, startDate.Value.Date, endDate.Value.Date)
                .Any(p => p.PhysicalRoomId == physicalRoomId))
                throw new HotelRuleException("Phòng không khả dụng, đã có lịch đặt hoặc lịch bảo trì trùng khoảng ngày.");
            var maintenance = new RoomMaintenance { PhysicalRoomId = physicalRoomId, StartDate = startDate.Value.Date,
                EndDate = endDate.Value.Date, Reason = reason, Note = note, Status = "Đang bảo trì", CreatedAt = DateTime.Now };
            db.RoomMaintenances.Add(maintenance); db.SaveChanges();
            return maintenance;
        }

        public static void EndMaintenance(HotelDbContext db, int id)
        {
            var maintenance = db.RoomMaintenances.Find(id);
            if (maintenance == null || maintenance.Status != "Đang bảo trì")
                throw new HotelRuleException("Lịch bảo trì không tồn tại hoặc đã kết thúc.");
            // Reservations and the calendar operate on days: release the current day.
            // Original StartDate/EndDate are retained for the audit history.
            maintenance.EndedAt = VietnamToday();
            maintenance.Status = "Đã kết thúc sớm";
            db.SaveChanges();
        }

    }
}

