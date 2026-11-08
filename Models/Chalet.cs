using System.ComponentModel.DataAnnotations;

namespace EaglesResort.Models
{
    public enum ChaletStatus
    {
        Available,      // متاح
        Occupied,       // مسكون
        Maintenance,    // تحت الصيانة
        Cleaning        // تنظيف
    }

    public class Chalet
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Number { get; set; } = string.Empty; // رقم الشاليه

        public string BuildingArea { get; set; } = string.Empty; // المبنى / المنطقة

        [Required]
        public string ChaletType { get; set; } = string.Empty; // نوع الشاليه (روفي مميز، عضوية مصايف...)

        public int RoomsCount { get; set; } // عدد الغرف/الأسرة

        public int MaxCapacity { get; set; } // السعة القصوى للأفراد

        public ChaletStatus Status { get; set; } = ChaletStatus.Available;

        public string Notes { get; set; } = string.Empty; // ملاحظات الصيانة/التجهيز

        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
