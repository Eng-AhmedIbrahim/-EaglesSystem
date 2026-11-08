using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EaglesResort.Models
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        [ForeignKey("BookingId")]
        public virtual Booking? Booking { get; set; }

        public string GroupBookingCode { get; set; } = string.Empty;

        // جميع الحجوزات المرتبطة بهذه الفاتورة
        [InverseProperty("Invoice")]
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        // ملخص الشاليهات المحجوزة
        public string ChaletsSummary { get; set; } = string.Empty;

        // بيانات الإقامة
        public decimal DailyRate { get; set; }          // سعر اليوم المطبق
        public decimal ChaletTotal { get; set; }         // إجمالي الشاليه (الأيام × سعر اليوم)

        // الوجبات
        public int AdultMealsCount { get; set; }         // عدد وجبات البالغين
        public decimal AdultMealRate { get; set; }       // سعر وجبة البالغ
        public int ChildMealsCount { get; set; }         // عدد وجبات الأطفال
        public decimal ChildMealRate { get; set; }       // سعر وجبة الطفل
        public decimal MealsTotal { get; set; }          // إجمالي الوجبات

        // إضافيات
        public decimal ExtraServices { get; set; }       // خدمات إضافية
        public string ExtraServicesNote { get; set; } = string.Empty;
        public decimal Insurance { get; set; }           // التأمين

        // المقدم
        public decimal Deposit { get; set; }             // مقدم الحجز
        public DateTime? DepositDate { get; set; }
        public string DepositReceiptImagePath { get; set; } = string.Empty;       // تاريخ المقدم

        // طريقة الدفع
        public string HousingPaymentMethod { get; set; } = "كاش"; // كاش / بنك
        public string MealsPaymentMethod { get; set; } = "كاش";   // كاش / تحويل

        // تفاصيل الدفع المجزأ
        public string PaymentDetails { get; set; } = string.Empty; // مثل: كاش: 1000 ج.م | انستا باي: 2000 ج.م
        public decimal CashAmount { get; set; }
        public decimal InstaPayAmount { get; set; }
        public decimal VisaAmount { get; set; }
        public decimal BankTransferAmount { get; set; }

        // Meals split payment
        public string MealsPaymentDetails { get; set; } = string.Empty;
        public decimal MealsCashAmount { get; set; }
        public decimal MealsInstaPayAmount { get; set; }
        public decimal MealsVisaAmount { get; set; }
        public decimal MealsBankTransferAmount { get; set; }

        // الإجماليات المحسوبة
        public decimal NetHousing { get; set; }          // صافي الإسكان = إجمالي الشاليه - مقدم الحجز
        public decimal GrandTotal { get; set; }          // إجمالي الفاتورة الكاملة (شامل التأمين)
        public decimal TotalAfterDeposit { get; set; }  // إجمالي بعد خصم العربون

        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        // بيانات الطباعة
        public string ReceptionistName { get; set; } = string.Empty;
        public string AccountantName { get; set; } = string.Empty;
        public string ResortManagerName { get; set; } = string.Empty;
    }
}
