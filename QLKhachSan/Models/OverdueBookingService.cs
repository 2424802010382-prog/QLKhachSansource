using System;
using System.Configuration;
using System.Data.Entity;
using System.Linq;

namespace QLKhachSan.Models
{
    public static class OverdueBookingService
    {
        private static readonly object SyncLock = new object();

        public static int ProcessOverdueBookings(HotelDbContext db)
        {
            lock (SyncLock)
            {
                int processedCount = 0;
                try
                {
                    DateTime now = DateTime.Now;

                    int deadlineHour = 23;
                    int deadlineMinute = 59;
                    int.TryParse(ConfigurationManager.AppSettings["CheckInDeadlineHour"], out deadlineHour);
                    int.TryParse(ConfigurationManager.AppSettings["CheckInDeadlineMinute"], out deadlineMinute);

                    using (var transaction = db.Database.BeginTransaction())
                    {
                        var candidateBookings = db.Bookings
                            .Where(b => (b.Status == "Chờ xác nhận" || b.Status == "Đã xác nhận")
                                     && DbFunctions.TruncateTime(b.CheckInDate) < DbFunctions.TruncateTime(now))
                            .ToList();

                        foreach (var b in candidateBookings)
                        {
                            DateTime checkInDeadline = b.CheckInDate.Date.AddHours(deadlineHour).AddMinutes(deadlineMinute).AddSeconds(59);
                            if (now > checkInDeadline)
                            {
                                bool isPaid = db.ThanhToans.Any(t => t.BookingId == b.BookingId && t.TrangThai == "Đã thanh toán");

                                if (isPaid)
                                {
                                    b.Status = "Đã hủy";
                                    b.SpecialRequest = $"[VẮNG MẶT - NO SHOW - ĐÃ THANH TOÁN - CẦN XỬ LÝ TÀI CHÍNH - Xử lý lúc {now:dd/MM/yyyy HH:mm}] " + (b.SpecialRequest ?? "");
                                }
                                else
                                {
                                    b.Status = "Đã hủy";
                                    b.SpecialRequest = $"[TỰ ĐỘNG HỦY DO QUÁ HẠN NHẬN PHÒNG - Xử lý lúc {now:dd/MM/yyyy HH:mm}] " + (b.SpecialRequest ?? "");
                                }

                                processedCount++;
                            }
                        }

                        if (processedCount > 0)
                        {
                            db.SaveChanges();
                            PhysicalRoomHelper.SyncPhysicalRoomStatuses(db);
                        }

                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("ProcessOverdueBookings Error: " + ex.Message);
                }

                return processedCount;
            }
        }
    }
}
