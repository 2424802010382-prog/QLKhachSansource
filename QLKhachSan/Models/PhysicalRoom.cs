using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKhachSan.Models
{
    [Table("PhysicalRooms")]
    public class PhysicalRoom
    {
        [Key]
        public int PhysicalRoomId { get; set; }

        public int RoomTypeId { get; set; }

        [Required]
        [StringLength(20)]
        public string RoomNumber { get; set; }

        public int Floor { get; set; }

        [StringLength(50)]
        public string Status { get; set; } // Trống, Đã đặt, Đang ở, Bảo trì

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey("RoomTypeId")]
        public virtual Room RoomType { get; set; }
    }
}
