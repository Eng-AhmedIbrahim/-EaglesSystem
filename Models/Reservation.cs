using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EaglesResort.Models
{
    public enum ReservationStatus
    {
        Upcoming,   // قادم / حجز مسبق قائم
        CheckedIn,  // تم التسكين وإصدار الفاتورة
        Cancelled   // ملغي
    }

    public class Reservation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string GuestName { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        public string SecondaryPhoneNumber { get; set; } = string.Empty;

        [Required]
        public int ChaletId { get; set; }

        [ForeignKey("ChaletId")]
        public virtual Chalet? Chalet { get; set; }

        [Required]
        public DateTime CheckInDate { get; set; }

        [Required]
        public DateTime CheckOutDate { get; set; }

        public int NumberOfDays { get; set; } = 1;

        public decimal DailyRate { get; set; }

        public decimal ChaletTotal { get; set; }

        public decimal Deposit { get; set; }

        public DateTime? DepositDate { get; set; }

        public string DepositReceiptImagePath { get; set; } = string.Empty;

        // يسحب تاريخ ووقت الحجز الفعلي تلقائياً بواسطة السيستم
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string Notes { get; set; } = string.Empty;

        public ReservationStatus Status { get; set; } = ReservationStatus.Upcoming;

        public int? InvoiceId { get; set; }

        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }
    }
}
