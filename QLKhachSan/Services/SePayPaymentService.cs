using System;
using System.Data;
using System.Linq;
using QLKhachSan.Models;

namespace QLKhachSan.Services
{
    public sealed class SePayReceipt
    {
        public string PayloadHash { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
    }

    public sealed class SePayPaymentService
    {
        public static bool NeedsReview(HotelDbContext db, string type, int id)
        {
            return db.Database.SqlQuery<int>(@"IF OBJECT_ID('dbo.SePayTransactions','U') IS NULL SELECT 0
                ELSE SELECT COUNT(*) FROM dbo.SePayTransactions WHERE OrderType=@p0 AND OrderId=@p1 AND Status='Review'", type, id).Single() > 0;
        }

        public SePayReceipt Process(HotelDbContext db, BankPaymentSettings settings, SePayTransaction t, string payloadHash, bool verifiedLive)
        {
            if (!settings.Ready || (settings.Mode == "Live" && !verifiedLive)
                || (settings.Mode == "Sandbox" && !db.Database.Connection.Database.StartsWith("SePaySandbox_", StringComparison.Ordinal)))
                throw new InvalidOperationException("Payment environment is not ready.");
            db.Database.CommandTimeout = 12;
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                // Same lock as manual payments, cancellations, food orders and room changes.
                HotelOperations.LockInventory(db);
                string mode = db.Database.SqlQuery<string>("SELECT Mode FROM dbo.SePayEnvironment WHERE Id=1").Single();
                if (mode != settings.Mode) throw new InvalidOperationException("Database payment environment mismatch.");
                var previous = db.Database.SqlQuery<SePayReceipt>("SELECT PayloadHash,Status,Reason FROM dbo.SePayTransactions WHERE SePayId=@p0", t.Id).SingleOrDefault();
                if (previous != null)
                {
                    if (previous.PayloadHash != payloadHash) throw new InvalidOperationException("Transaction ID collision.");
                    tx.Commit(); return previous;
                }
                var result = new SePayReceipt { PayloadHash = payloadHash, Status = "Review", Reason = "UnmatchedContent" };
                string type; int orderId;
                bool matched = SePayProtocol.TryOrder(t.Content, out type, out orderId);
                int? paymentId = null;
                string reference = string.IsNullOrWhiteSpace(t.Reference) ? null : t.Reference.Trim();
                string receiptCode = "SEPAY:" + t.Id;
                if (t.TransferType != "in") { result.Status = "Ignored"; result.Reason = "Outgoing"; }
                else if (!string.Equals(t.Gateway, settings.BankName, StringComparison.OrdinalIgnoreCase) || t.AccountNumber != settings.AccountNumber)
                    result.Reason = "WrongAccountOrBank";
                else if (!matched) result.Reason = "UnmatchedContent";
                else if (reference == null) result.Reason = "MissingBankReference";
                else if (HotelOperations.RoomServiceReferenceUsed(db, reference, receiptCode)) result.Reason = "ReferenceAlreadyRecorded";
                else if (db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM dbo.SePayTransactions WHERE Bank=@p0 AND AccountNumber=@p1 AND BankReference=@p2", t.Gateway, t.AccountNumber, reference).Single() > 0
                    || db.ThanhToans.Any(p => p.TrangThai == "Đã thanh toán" && (p.MaGiaoDich == reference || p.MaGiaoDich == receiptCode))
                    || db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.DonDatMon WHERE TrangThaiThanhToan=N'Đã thanh toán' AND (PaymentReference=@p0 OR PaymentReference=@p1)", reference, receiptCode).Single() > 0)
                    result.Reason = "ReferenceAlreadyRecorded";
                else if (type == "Booking")
                {
                    var booking = db.Bookings.SingleOrDefault(b => b.BookingId == orderId);
                    if (booking == null) result.Reason = "OrderNotFound";
                    else if (!HotelOperations.ActiveStatuses.Contains(booking.Status)) result.Reason = "InactiveOrder";
                    else if (booking.CreatedAt > t.TransactionDate) result.Reason = "TransactionBeforeOrder";
                    else
                    {
                        decimal paid = db.ThanhToans.Where(p => p.BookingId == orderId && p.TrangThai == "Đã thanh toán").Select(p => (decimal?)p.SoTien).Sum() ?? 0;
                        decimal due = booking.TotalAmount - paid;
                        if (due <= 0) result.Reason = "AlreadyPaid";
                        else if (t.Amount != due) result.Reason = t.Amount < due ? "Underpayment" : "Overpayment";
                        else
                        {
                            booking.PaymentMethod = "Chuyển khoản";
                            var payment = HotelOperations.SynchronizeOutstandingPayment(db, booking);
                            payment.TrangThai = "Đã thanh toán";
                            payment.MaGiaoDich = receiptCode;
                            payment.NgayThanhToan = t.TransactionDate;
                            payment.GhiChu = "SePay HMAC; giao dịch #" + t.Id + "; NH: " + reference;
                            db.SaveChanges();
                            paymentId = payment.MaThanhToan;
                            db.Database.ExecuteSqlCommand(@"UPDATE dbo.DonDatMon SET TrangThaiThanhToan=N'Đã thanh toán',PaidAt=@p0,PaymentEvidence=@p1
                                WHERE BookingId=@p2 AND PayWithBooking=1 AND TrangThai<>N'Đã hủy' AND TrangThaiThanhToan<>N'Đã thanh toán'",
                                t.TransactionDate, "Thu cùng hóa đơn phòng; " + receiptCode, orderId);
                            result.Status = "Applied"; result.Reason = "Paid";
                        }
                    }
                }
                else if (type == "RoomService")
                {
                    var order = db.Database.SqlQuery<RoomServiceOrder>(@"SELECT o.*,p.RoomNumber,b.CustomerName
                        FROM dbo.RoomServiceOrders o
                        INNER JOIN dbo.PhysicalRooms p ON p.PhysicalRoomId=o.PhysicalRoomId
                        INNER JOIN dbo.Bookings b ON b.BookingId=o.BookingId
                        WHERE o.OrderId=@p0", orderId).SingleOrDefault();
                    if (order == null) result.Reason = "OrderNotFound";
                    else if (order.Status == "Từ chối") result.Reason = "InactiveOrder";
                    else if (order.PaymentStatus == "Đã thanh toán") result.Reason = "AlreadyPaid";
                    else if (order.CreatedAt > t.TransactionDate) result.Reason = "TransactionBeforeOrder";
                    else if (t.Amount != order.Total) result.Reason = t.Amount < order.Total ? "Underpayment" : "Overpayment";
                    else
                    {
                        int changed = db.Database.ExecuteSqlCommand(@"UPDATE dbo.RoomServiceOrders SET PaymentStatus=N'Đã thanh toán',
                            PaymentMethod=N'Chuyển khoản',PaidAt=@p0,Evidence=@p1
                            WHERE OrderId=@p2 AND Status<>N'Từ chối' AND PaymentStatus<>N'Đã thanh toán'",
                            t.TransactionDate, receiptCode, orderId);
                        if (changed != 1) throw new InvalidOperationException("Concurrent payment change.");
                        result.Status = "Applied"; result.Reason = "Paid";
                    }
                }
                else
                {
                    var order = HotelOperations.FoodOrder(db, orderId);
                    if (order == null) result.Reason = "OrderNotFound";
                    else if (order.PayWithBooking) result.Reason = "PayWithBooking";
                    else if (order.TrangThai == "Đã hủy") result.Reason = "InactiveOrder";
                    else if (order.TrangThaiThanhToan == "Đã thanh toán") result.Reason = "AlreadyPaid";
                    else if (db.Database.SqlQuery<DateTime>("SELECT CreatedAt FROM dbo.DonDatMon WHERE MaDon=@p0", orderId).Single() > t.TransactionDate) result.Reason = "TransactionBeforeOrder";
                    else if (t.Amount != order.TongTien) result.Reason = t.Amount < order.TongTien ? "Underpayment" : "Overpayment";
                    else
                    {
                        int changed = db.Database.ExecuteSqlCommand(@"UPDATE dbo.DonDatMon SET TrangThaiThanhToan=N'Đã thanh toán',
                            PhuongThucThanhToan=N'Chuyển khoản',PaidAt=@p0,PaymentReference=@p1,PaymentEvidence=@p2
                            WHERE MaDon=@p3 AND PayWithBooking=0 AND TrangThai<>N'Đã hủy' AND TrangThaiThanhToan<>N'Đã thanh toán'",
                            t.TransactionDate, receiptCode, "SePay HMAC; NH: " + reference, orderId);
                        if (changed != 1) throw new InvalidOperationException("Concurrent payment change.");
                        result.Status = "Applied"; result.Reason = "Paid";
                    }
                }
                db.Database.ExecuteSqlCommand(@"INSERT dbo.SePayTransactions
                    (SePayId,Mode,Bank,AccountNumber,BankReference,OrderType,OrderId,Amount,Direction,Status,Reason,TransactionDate,PayloadHash,PaymentId)
                    VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",
                    t.Id, settings.Mode, t.Gateway, t.AccountNumber, (object)reference ?? DBNull.Value,
                    matched ? (object)type : DBNull.Value, matched ? (object)orderId : DBNull.Value,
                    t.Amount, t.TransferType, result.Status, result.Reason, t.TransactionDate, payloadHash, (object)paymentId ?? DBNull.Value);
                tx.Commit();
                return result;
            }
        }
    }
}

