using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace QLKhachSan.Models
{
    // Both dashboards use the same day, assignment and maintenance rules.
    public static class RoomMatrixService
    {
        public static DateTime ParseDate(string value)
        {
            DateTime date;
            return DateTime.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out date) && date < DateTime.MaxValue.Date
                ? date.Date : DateTime.Today;
        }

        public static object Load(HotelDbContext db, DateTime date, bool bookingDetails = true)
        {
            date = date.Date;
            var end = date.AddDays(1);
            var rooms = db.PhysicalRooms.AsNoTracking().Include(p => p.RoomType)
                .OrderBy(p => p.Floor).ThenBy(p => p.RoomNumber).ToList();
            var bookings = db.Bookings.AsNoTracking().Where(b =>
                HotelOperations.ActiveStatuses.Contains(b.Status) || (date < DateTime.Today && b.Status == "Đã trả phòng" && b.CheckOutDate > date))
                .OrderBy(b => b.CheckInDate).ThenBy(b => b.BookingId).ToList();
            var maintenance = db.RoomMaintenances.AsNoTracking().OrderByDescending(m => m.StartDate).ToList();
            var todayBookings = bookings.Where(b => b.CheckInDate < end && b.CheckOutDate > date).ToList();
            var ids = todayBookings.Select(b => b.BookingId).ToList();
            var paid = bookingDetails ? new HashSet<int>(db.Bookings.AsNoTracking().Where(b => ids.Contains(b.BookingId) &&
                (b.ThanhToans.Where(t => t.TrangThai == "Đã thanh toán").Select(t => (decimal?)t.SoTien).Sum() ?? 0) >= b.TotalAmount)
                .Select(b => b.BookingId).ToList()) : new HashSet<int>();
            var result = new List<object>();
            foreach (var room in rooms)
            {
                var status = HotelOperations.RoomStatus(room, todayBookings, maintenance, date);
                var matched = todayBookings.Where(b => b.RoomId == room.RoomTypeId &&
                    PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, room.RoomNumber.Trim()))
                    .OrderBy(b => b.Status == "Đang ở" ? 0 : b.Status == "Đã xác nhận" ? 1 : b.Status == "Chờ xác nhận" ? 2 : 3)
                    .ThenBy(b => b.BookingId).FirstOrDefault();
                var history = maintenance.Where(m => m.PhysicalRoomId == room.PhysicalRoomId).ToList();
                var active = history.FirstOrDefault(m => HotelOperations.MaintenanceOverlaps(m, date, end));
                result.Add(new
                {
                    physicalRoomId = room.PhysicalRoomId, roomNumber = room.RoomNumber, floor = room.Floor,
                    roomTypeName = room.RoomType == null ? "Phòng" : room.RoomType.RoomName, status,
                    currentBooking = !bookingDetails || matched == null || status == "Bảo trì" || status == "Ngừng hoạt động" ? null : new
                    {
                        bookingId = matched.BookingId, isProvisionalAssignment = false,
                        customerName = string.IsNullOrWhiteSpace(matched.CustomerName) ? "Khách hàng" : matched.CustomerName,
                        customerPhone = matched.CustomerPhone ?? "Chưa cung cấp", customerEmail = matched.CustomerEmail ?? "Chưa cung cấp",
                        checkInDate = matched.CheckInDate.ToString("dd/MM/yyyy"), checkOutDate = matched.CheckOutDate.ToString("dd/MM/yyyy"),
                        status = matched.Status, totalAmount = matched.TotalAmount.ToString("N0") + "đ", isPaid = paid.Contains(matched.BookingId)
                    },
                    currentMaintenance = active == null ? null : new
                    {
                        maintenanceId = active.MaintenanceId, reason = active.Reason, note = active.Note, status = active.Status,
                        startDate = active.StartDate.ToString("dd/MM/yyyy"), endDate = HotelOperations.MaintenanceEnd(active).ToString("dd/MM/yyyy")
                    },
                    maintenanceHistory = history.Select(m => new
                    {
                        maintenanceId = m.MaintenanceId, reason = m.Reason, status = m.Status,
                        startDate = m.StartDate.ToString("dd/MM/yyyy"), endDate = HotelOperations.MaintenanceEnd(m).ToString("dd/MM/yyyy")
                    }).ToList(),
                    upcomingBookings = bookings.Where(b => bookingDetails && b.CheckInDate >= end && b.RoomId == room.RoomTypeId &&
                        HotelOperations.ActiveStatuses.Contains(b.Status) && PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, room.RoomNumber.Trim()))
                        .Take(5).Select(b => new
                        {
                            bookingId = b.BookingId, customerName = b.CustomerName ?? "Khách hàng",
                            checkInDate = b.CheckInDate.ToString("dd/MM/yyyy"), checkOutDate = b.CheckOutDate.ToString("dd/MM/yyyy"), status = b.Status
                        }).ToList()
                });
            }
            // A reservation without a valid assignment reserves capacity, not a made-up room number.
            var unassigned = bookings.Where(b => bookingDetails && HotelOperations.ActiveStatuses.Contains(b.Status))
                .Select(b => new { Booking = b, Missing = Math.Max(0, b.RoomCount - rooms.Count(p => p.RoomTypeId == b.RoomId &&
                    PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, p.RoomNumber.Trim()))) })
                .Where(b => b.Missing > 0).Select(b => new
                {
                    bookingId = b.Booking.BookingId, roomTypeName = rooms.Where(p => p.RoomTypeId == b.Booking.RoomId)
                        .Select(p => p.RoomType == null ? "Phòng" : p.RoomType.RoomName).FirstOrDefault() ?? "Loại phòng #" + b.Booking.RoomId,
                    missingRoomCount = b.Missing, checkInDate = b.Booking.CheckInDate.ToString("dd/MM/yyyy"),
                    checkOutDate = b.Booking.CheckOutDate.ToString("dd/MM/yyyy"), status = b.Booking.Status
                }).ToList();
            return new { success = true, selectedDate = date.ToString("yyyy-MM-dd"), displayDate = date.ToString("dd/MM/yyyy"),
                rooms = result, unassignedBookings = unassigned };
        }
    }
}
