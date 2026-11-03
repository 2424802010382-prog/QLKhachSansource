using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Data.Entity;

namespace QLKhachSan.Models
{
    public static class PhysicalRoomHelper
    {
        public static bool IsBookingAssignedToRoom(string specialRequest, string targetRoomNumber)
        {
            if (string.IsNullOrEmpty(specialRequest)) return false;
            try
            {
                int idx = 0;
                while ((idx = specialRequest.IndexOf("[Phòng:", idx, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    int start = idx + 7;
                    int end = specialRequest.IndexOf("]", start);
                    if (end > start)
                    {
                        string rNum = specialRequest.Substring(start, end - start).Trim();
                        if (string.Equals(rNum, targetRoomNumber, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    idx = start;
                }
            }
            catch { }
            return false;
        }

        public static void SyncPhysicalRoomStatuses(HotelDbContext db)
        {
            try
            {
                DateTime today = DateTime.Today;

                var physicalRooms = db.PhysicalRooms.ToList();
                var activeBookings = db.Bookings
                    .Where(b => (b.Status == "Đang ở" || b.Status == "Đã xác nhận" || b.Status == "Chờ xác nhận") && DbFunctions.TruncateTime(b.CheckOutDate) >= today)
                    .ToList();

                foreach (var pr in physicalRooms)
                {
                    if (pr.Status == "Bảo trì") continue;

                    string roomNum = pr.RoomNumber.Trim();
                    var matchedBooking = activeBookings.FirstOrDefault(b =>
                        b.RoomId == pr.RoomTypeId &&
                        !string.IsNullOrEmpty(b.SpecialRequest) &&
                        IsBookingAssignedToRoom(b.SpecialRequest, roomNum)
                    );

                    string newStatus = "Trống";
                    if (matchedBooking != null)
                    {
                        if (matchedBooking.Status == "Đang ở") newStatus = "Đang ở";
                        else if (matchedBooking.Status == "Đã xác nhận") newStatus = "Đã đặt";
                        else if (matchedBooking.Status == "Chờ xác nhận") newStatus = "Chờ xác nhận";
                    }

                    if (pr.Status != newStatus)
                    {
                        pr.Status = newStatus;
                    }
                }

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SyncPhysicalRoomStatuses Error: " + ex.Message);
            }
        }
    }


    // =====================================================
    // BẢNG QUYỀN HẠN (PERMISSIONS)
    // =====================================================

    [Table("Permissions")]
    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }

        [Required]
        [StringLength(100)]
        public string PermissionCode { get; set; }

        [Required]
        [StringLength(200)]
        public string PermissionName { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; }
    }


    // =====================================================
    // BẢNG PHÂN QUYỀN NHÂN VIÊN (USER PERMISSIONS)
    // =====================================================

    [Table("UserPermissions")]
    public class UserPermission
    {
        [Key]
        public int UserPermissionId { get; set; }

        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string PermissionCode { get; set; }

        public DateTime GrantedAt { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
}
