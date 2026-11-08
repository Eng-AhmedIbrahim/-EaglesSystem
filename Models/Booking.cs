using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EaglesResort.Models
{
    public enum BookingStatus
    {
        Active,     // نشط
        CheckedOut, // تمت المغادرة
        Cancelled,  // ملغي
        Upcoming    // قادم
    }

    public class Booking
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ChaletId { get; set; }

        [ForeignKey("ChaletId")]
        public virtual Chalet? Chalet { get; set; }

        [Required]
        public string GuestName { get; set; } = string.Empty; // اسم النزيل

        public string PhoneNumber { get; set; } = string.Empty; // رقم الهاتف
        public string SecondaryPhoneNumber { get; set; } = string.Empty; // رقم هاتف إضافي

        public string GuestRank { get; set; } = string.Empty; // الرتبة التفصيلية

        public string GuestCategory { get; set; } = "مدني"; // فئة النزيل للحسابات
        
        public string PaymentSystem { get; set; } = "دفع إدارة"; // منظومة الدفع
        
        public string MealPlan { get; set; } = "وجبات طول الحجز"; // حالة الوجبات

        public int AdultCount { get; set; } = 1; // عدد الأفراد البالغين
        public int ChildCount { get; set; } = 0; // عدد الأطفال

        public bool HasResortMembership { get; set; } // عضوية مصايف (يوجد/لا يوجد)
        public string MembershipNumber { get; set; } = string.Empty; // رقم العضوية

        [Required]
        public DateTime CheckInDate { get; set; } // تاريخ الوصول

        [Required]
        public DateTime CheckOutDate { get; set; } // تاريخ المغادرة

        public decimal DailyRate { get; set; } // سعر اليوم الخاص بهذا الشاليه
        public decimal ChaletTotal { get; set; } // إجمالي الإقامة بهذا الشاليه
        public decimal Deposit { get; set; } // ديبوزت (مقدم) خاص بهذا الشاليه
        public DateTime? DepositDate { get; set; } // تاريخ الديبوزت

        public int? InvoiceId { get; set; } // الفاتورة المجمعة المرتبطة
        public string GroupBookingCode { get; set; } = string.Empty; // كود تجميع الحجوزات معاً

        public BookingStatus Status { get; set; } = BookingStatus.Upcoming;

        public string MealStatus { get; set; } = string.Empty; // حالة الوجبات (الوجبات طول الحجز، الخ)

        public string Notes { get; set; } = string.Empty;

        public bool InsurancePaid { get; set; } = false;
        public string DepositReceiptImagePath { get; set; } = string.Empty; // هل دفع التأمين

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }

        // Computed
        [NotMapped]
        public int NumberOfDays => (CheckOutDate - CheckInDate).Days;
    }
}
