using System.ComponentModel.DataAnnotations;

namespace EaglesResort.Models
{
    public class SpecialRate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ChaletType { get; set; } = string.Empty; // نوع الشاليه

        [Required]
        public string GuestCategory { get; set; } = string.Empty; // الفئة: عسكري قوات مسلحة، قوات جوية، إلخ

        public decimal DailyRate { get; set; }

        public decimal AdultMealRate { get; set; }

        public decimal ChildMealRate { get; set; }
    }
}
