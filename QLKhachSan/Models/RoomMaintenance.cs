using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKhachSan.Models
{
    [Table("RoomMaintenances")]
    public class RoomMaintenance
    {
        [Key]
        public int MaintenanceId { get; set; }

        public int PhysicalRoomId { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; }

        public string Note { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } // Đang bảo trì, Đã hoàn tất, Đã hủy

        public DateTime CreatedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        [ForeignKey("PhysicalRoomId")]
        public virtual PhysicalRoom PhysicalRoom { get; set; }
    }
}
