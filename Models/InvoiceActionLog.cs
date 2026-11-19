using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EaglesResort.Models
{
    /// <summary>
    /// سجل حركات الفواتير: تعديل، إلغاء، طباعة، مغادرة... إلخ.
    /// كل حركة تُحفظ في قاعدة البيانات للرجوع إليها لاحقاً.
    /// </summary>
    public class InvoiceActionLog
    {
        [Key]
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        public int? BookingId { get; set; }

        // Edited / Cancelled / Printed / CheckedOut / Extended
        public string ActionType { get; set; } = string.Empty;

        // العنوان العربي اللي يظهر للمستخدم
        public string ActionTitle { get; set; } = string.Empty;

        public string PerformedBy { get; set; } = string.Empty;
        public DateTime PerformedAt { get; set; } = DateTime.Now;

        // تفاصيل التغيير (قبل/بعد، سبب الإلغاء...)
        public string Details { get; set; } = string.Empty;

        [NotMapped]
        public string PerformedAtDisplay => PerformedAt.ToString("dd/MM/yyyy hh:mm tt");

        [NotMapped]
        public bool IsDanger => ActionType == "Cancelled";

        [NotMapped]
        public bool IsEdited => ActionType == "Edited";
    }
}
