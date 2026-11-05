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
            // Only write operations refresh this cache; date-based screens never write.
            var snapshots = HotelOperations.RoomSnapshot(db, DateTime.Today).ToDictionary(p => p.PhysicalRoomId);
            var maintenanceHistory = db.RoomMaintenances.AsNoTracking().ToList();
            foreach (var room in db.PhysicalRooms.ToList())
            {
                if (HotelOperations.ManualMaintenance(room, maintenanceHistory)) continue;
                var status = snapshots[room.PhysicalRoomId].Status;
                room.Status = status == "Bảo trì" ? "Bảo trì theo lịch" : status;
            }
            db.SaveChanges();
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
