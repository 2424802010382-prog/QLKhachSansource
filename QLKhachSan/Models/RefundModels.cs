using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QLKhachSan.Models
{
    [Table("RefundRequests")]
    public class RefundRequest
    {
        [Key]
        public int RefundRequestId { get; set; }

        [Required]
        public int BookingId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; }

        [Required]
        [StringLength(20)]
        public string CustomerPhone { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerEmail { get; set; }

        [Required]
        public decimal PaidAmount { get; set; }

        [Required]
        public decimal RefundRate { get; set; }

        [Required]
        public decimal RefundAmount { get; set; }

        [Required]
        [StringLength(100)]
        public string BankName { get; set; }

        [Required]
        [StringLength(50)]
        public string AccountNumber { get; set; }

        [Required]
        [StringLength(100)]
        public string AccountHolder { get; set; }

        [Required]
        public string Reason { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } // ChoDuyet, NhanVienDaDuyet, DaHoanTien, TuChoi

        [Required]
        public DateTime RequestedAt { get; set; }

        public int? StaffUserId { get; set; }

        public DateTime? StaffVerifiedAt { get; set; }

        public int? AdminUserId { get; set; }

        public DateTime? AdminApprovedAt { get; set; }

        public string RejectedReason { get; set; }

        [ForeignKey("BookingId")]
        public virtual Booking Booking { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [ForeignKey("StaffUserId")]
        public virtual User StaffUser { get; set; }

        [ForeignKey("AdminUserId")]
        public virtual User AdminUser { get; set; }

        public virtual ICollection<RefundTransaction> RefundTransactions { get; set; }
    }

    [Table("RefundTransactions")]
    public class RefundTransaction
    {
        [Key]
        public int RefundTransactionId { get; set; }

        [Required]
        public int RefundRequestId { get; set; }

        [Required]
        public int BookingId { get; set; }

        [Required]
        [StringLength(100)]
        public string TransactionCode { get; set; }

        [StringLength(500)]
        public string ReceiptImageUrl { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime ProcessedAt { get; set; }

        [Required]
        public int ProcessedByUserId { get; set; }

        public string Notes { get; set; }

        [ForeignKey("RefundRequestId")]
        public virtual RefundRequest RefundRequest { get; set; }

        [ForeignKey("BookingId")]
        public virtual Booking Booking { get; set; }

        [ForeignKey("ProcessedByUserId")]
        public virtual User ProcessedByUser { get; set; }
    }

    public class CreateRefundRequestInputModel
    {
        public int BookingId { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountHolder { get; set; }
        public string Reason { get; set; }
    }

    public class StaffRefundListViewModel
    {
        public int RefundRequestId { get; set; }
        public int BookingId { get; set; }
        public string RoomName { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmail { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RefundRate { get; set; }
        public decimal RefundAmount { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountHolder { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public string RejectedReason { get; set; }
    }

    public class AdminRefundManagementViewModel
    {
        public List<StaffRefundListViewModel> Requests { get; set; }
        public decimal TotalRefundedAmount { get; set; }
        public int PendingCount { get; set; }
        public int StaffApprovedCount { get; set; }
        public int CompletedCount { get; set; }
        public int RejectedCount { get; set; }
    }
}
