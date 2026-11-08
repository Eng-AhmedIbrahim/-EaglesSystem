using System.ComponentModel.DataAnnotations;

namespace EaglesResort.Models
{
    public class Rate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ChaletType { get; set; } = string.Empty; // نوع الشاليه

        [Required]
        public int Month { get; set; } // 1-12

        public decimal DailyRate { get; set; } // سعر اليوم

        public decimal AdultMealRate { get; set; } // سعر وجبة البالغ

        public decimal ChildMealRate { get; set; } // سعر وجبة الطفل
    }
}
