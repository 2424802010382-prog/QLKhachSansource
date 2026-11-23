using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QLKhachSan.Models
{
    public static class RoomServiceImages
    {
        private static readonly HashSet<string> Codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CLEAN", "LAUNDRY", "IRON", "EXTRA_BED", "BLANKET", "BIRTHDAY", "ANNIVERSARY",
            "FLOWERS", "CAKE", "MINIBAR", "DRYCLEAN", "LINEN", "AMENITIES", "LUGGAGE", "WAKEUP"
        };

        // Presentation fallback only. Preserve custom Admin images and all database values.
        public static string Resolve(RoomServiceItem service)
        {
            if (Codes.Contains(service.Code ?? ""))
            {
                return "~/Images/RoomServices/" + service.Code.ToLowerInvariant() + ".svg";
            }
            string configured = (service.ImageUrl ?? "").Trim();
            if (configured.Length > 0 && !configured.Equals("/Images/room-service.svg", StringComparison.OrdinalIgnoreCase)
                && !configured.Equals("~/Images/room-service.svg", StringComparison.OrdinalIgnoreCase)) return configured;
            return "~/Images/room-service.svg";
        }
    }

    // SQL projections: deliberately not added to the legacy EDMX or EF entity model.
    public class RoomServiceItem
    {
        private string _tenDichVu;
        private string _moTa;
        public int MaDichVu { get; set; }
        public string Code { get; set; }
        public string TenDichVu { get => _tenDichVu; set => _tenDichVu = value?.Normalize(System.Text.NormalizationForm.FormC); }
        public string MoTa { get => _moTa; set => _moTa = value?.Normalize(System.Text.NormalizationForm.FormC); }
        public decimal Gia { get; set; }
        public string DonVi { get; set; }
        public bool HoatDong { get; set; }
        public string ImageUrl { get; set; }
        public string Timing { get; set; }
    }
    public partial class RoomServiceProduct
    {
    }
    public class RoomServiceRoom
    {
        private string _label;
        public int BookingId { get; set; }
        public int PhysicalRoomId { get; set; }
        public int UserId { get; set; }
        public string Label { get => _label; set => _label = value?.Normalize(System.Text.NormalizationForm.FormC); }
    }
    public class RoomServiceRequest
    {
        private string _note;
        public int ServiceId { get; set; }
        public int BookingId { get; set; }
        public int PhysicalRoomId { get; set; }
        public int? ProductId { get; set; }
        [Range(1, 30)] public int Quantity { get; set; }
        public DateTime UseAt { get; set; }
        [StringLength(1000)] public string Note { get => _note; set => _note = value?.Normalize(System.Text.NormalizationForm.FormC); }
        public string PaymentMethod { get; set; }
        public Guid RequestKey { get; set; }
        public RoomServiceItem Service { get; set; }
        public List<RoomServiceRoom> Rooms { get; set; }
        public List<RoomServiceProduct> Products { get; set; }
    }
    public partial class RoomServiceOrder
    {
        private string _roomNumber, _customerName;
        public string RoomNumber { get => _roomNumber; set => _roomNumber = value?.Normalize(System.Text.NormalizationForm.FormC); }
        public string CustomerName { get => _customerName; set => _customerName = value?.Normalize(System.Text.NormalizationForm.FormC); }
    }
    public class RoomServiceEvent
    {
        private string _message;
        public DateTime CreatedAt { get; set; }
        public int ActorId { get; set; }
        public string Message { get => _message; set => _message = value?.Normalize(System.Text.NormalizationForm.FormC); }
    }
    public static class RoomServiceRules
    {
        public static bool CanUseRoom(Booking booking, PhysicalRoom room, int owner, int actor, bool staff)
        {
            return actor > 0 && owner > 0 && (staff || owner == actor) && booking != null && room != null &&
                room.IsActive && room.RoomTypeId == booking.RoomId &&
                PhysicalRoomHelper.IsBookingAssignedToRoom(booking.SpecialRequest, room.RoomNumber.Trim());
        }
        public static bool Eligible(Booking b, string timing, decimal paid, DateTime now, DateTime at)
        {
            if (b == null || at < now || b.CheckOutDate <= b.CheckInDate ||
                (b.Status != "Đã xác nhận" && b.Status != "Đang ở")) return false;
            if (timing == "Luggage")
                return (at.Date == b.CheckInDate.Date || at.Date == b.CheckOutDate.Date) &&
                    now.Date <= b.CheckOutDate.Date && (b.Status == "Đang ở" || b.Status == "Đã xác nhận" || at.Date == b.CheckInDate.Date);
            if (at.Date < b.CheckInDate.Date || at >= b.CheckOutDate.Date) return false;
            if (timing == "BeforeArrival") return now < b.CheckOutDate.Date;
            return timing == "Stay" && (b.Status == "Đang ở" || b.Status == "Đã xác nhận") &&
                now >= b.CheckInDate.Date && now < b.CheckOutDate.Date;
        }
        public static bool CanTransition(string from, string to)
        {
            return (from == "Chờ xác nhận" && (to == "Đã xác nhận" || to == "Từ chối")) ||
                (from == "Đã xác nhận" && to == "Đang thực hiện") || (from == "Đang thực hiện" && to == "Hoàn thành");
        }
    }
}
