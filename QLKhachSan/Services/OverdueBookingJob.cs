using System;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using QLKhachSan.Models;

namespace QLKhachSan.Services
{
    public static class OverdueBookingJob
    {
        // Long random secret configured in Web.config appSettings on each environment.
        // Do NOT hardcode tokens into the repository.
        public static bool IsValidToken(string supplied)
        {
            string expected = ConfigurationManager.AppSettings["OverdueJobToken"];
            if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32 ||
                string.IsNullOrWhiteSpace(supplied)) return false;
            byte[] a, b;
            using (var sha = SHA256.Create())
            {
                a = sha.ComputeHash(Encoding.UTF8.GetBytes(expected));
                b = sha.ComputeHash(Encoding.UTF8.GetBytes(supplied));
            }
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static DateTime VietnamToday()
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
        }

        public static JobResult Run(string connectionString = null)
        {
            string cs = string.IsNullOrEmpty(connectionString)
                ? ConfigurationManager.ConnectionStrings["HotelDbContext"]?.ConnectionString
                : connectionString;

            if (string.IsNullOrEmpty(cs))
            {
                throw new System.Configuration.ConfigurationErrorsException("Không tìm thấy cấu hình connection string 'HotelDbContext' trong Web.config.");
            }

            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "dbo.HotelAutoCancelOverdueBookings";
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@VietnamToday", SqlDbType.Date) { Value = VietnamToday() });
                    var pCancel = new SqlParameter("@CancelledCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    var pPaid = new SqlParameter("@PaidReviewCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(pCancel);
                    cmd.Parameters.Add(pPaid);

                    cmd.ExecuteNonQuery();

                    int cancelled = pCancel.Value != DBNull.Value ? Convert.ToInt32(pCancel.Value) : 0;
                    int paid = pPaid.Value != DBNull.Value ? Convert.ToInt32(pPaid.Value) : 0;

                    return new JobResult { Cancelled = cancelled, PaidReview = paid };
                }
            }
        }

        public static bool RunTestScenario(string connectionString)
        {
            Database.SetInitializer<HotelDbContext>(null);
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM dbo.HotelAutoCancelAudit WHERE BookingId = 9990; DELETE FROM dbo.Bookings WHERE BookingId = 9990;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"SET IDENTITY_INSERT dbo.Bookings ON;
                        INSERT INTO dbo.Bookings (BookingId, RoomId, CustomerName, CheckInDate, CheckOutDate, Status, TotalAmount, RoomCount, GuestCount, CreatedAt)
                        VALUES (9990, 1, @name, @checkin, @checkout, @status, 1000000, 1, 2, GETDATE());
                        SET IDENTITY_INSERT dbo.Bookings OFF;";
                    cmd.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 200) { Value = "Test CSharp Guest" });
                    cmd.Parameters.Add(new SqlParameter("@checkin", SqlDbType.DateTime) { Value = new DateTime(2026, 10, 1) });
                    cmd.Parameters.Add(new SqlParameter("@checkout", SqlDbType.DateTime) { Value = new DateTime(2026, 10, 5) });
                    cmd.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 100) { Value = "Đã xác nhận" });
                    cmd.ExecuteNonQuery();
                }
            }

            var res = Run(connectionString);
            return res.Cancelled > 0;
        }

        public sealed class JobResult
        {
            public int Cancelled { get; set; }
            public int PaidReview { get; set; }
        }
    }
}
