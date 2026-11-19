using System;
using System.Collections.Generic;
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

        // الرقم التسلسلي للفاتورة (يظهر أعلى الفاتورة)
        public string InvoiceNumber { get; set; } = string.Empty;

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

        // ===== حالة الفاتورة وسجل التعديل/الإلغاء =====
        public bool IsCancelled { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string CancelledBy { get; set; } = string.Empty;
        public string CancellationReason { get; set; } = string.Empty;

        public DateTime? LastModifiedAt { get; set; }
        public string LastModifiedBy { get; set; } = string.Empty;

        // ===== قيم محسوبة غير مخزنة في قاعدة البيانات =====
        [NotMapped] public bool IsModified => LastModifiedAt.HasValue && !IsCancelled;
        [NotMapped] public bool HasStatusNote => IsCancelled || LastModifiedAt.HasValue;

        [NotMapped] public string StatusText => IsCancelled
            ? "فاتورة ملغية"
            : (LastModifiedAt.HasValue ? "فاتورة معدلة" : string.Empty);

        [NotMapped] public string StatusDetailsDisplay
        {
            get
            {
                if (IsCancelled)
                {
                    var reason = string.IsNullOrWhiteSpace(CancellationReason) ? "بدون سبب مسجل" : CancellationReason;
                    var when = CancelledAt?.ToString("dd/MM/yyyy hh:mm tt") ?? "—";
                    return $"أُلغيت بواسطة {CancelledBy} في {when} — السبب: {reason}";
                }

                if (LastModifiedAt.HasValue)
                    return $"آخر تعديل بواسطة {LastModifiedBy} في {LastModifiedAt.Value:dd/MM/yyyy hh:mm tt}";

                return string.Empty;
            }
        }

        // سجل الحركات (يُحمَّل عند العرض فقط)
        [NotMapped] public List<InvoiceActionLog> ActionLogs { get; set; } = new();
        [NotMapped] public bool HasActionLogs => ActionLogs != null && ActionLogs.Count > 0;

        [NotMapped] public decimal HousingPaid => CashAmount + InstaPayAmount + VisaAmount + BankTransferAmount;
        [NotMapped] public decimal MealsPaid => MealsCashAmount + MealsInstaPayAmount + MealsVisaAmount + MealsBankTransferAmount;
        [NotMapped] public decimal TotalPaid => HousingPaid + MealsPaid;

        // إجمالي كل طريقة دفع (سكن + وجبات)
        [NotMapped] public decimal CashCombined => CashAmount + MealsCashAmount;
        [NotMapped] public decimal VisaCombined => VisaAmount + MealsVisaAmount;
        [NotMapped] public decimal InstaPayCombined => InstaPayAmount + MealsInstaPayAmount;
        [NotMapped] public decimal BankTransferCombined => BankTransferAmount + MealsBankTransferAmount;
        [NotMapped] public decimal RemainingAmount => Math.Max(0, TotalAfterDeposit - TotalPaid);
        [NotMapped] public bool HasPaymentSplit => TotalPaid > 0;

        [NotMapped] public string InvoiceNumberDisplay =>
            string.IsNullOrWhiteSpace(InvoiceNumber) ? (Id > 0 ? $"INV-{Id}" : "—") : InvoiceNumber;
    }
}
