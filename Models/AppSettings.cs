using System.ComponentModel.DataAnnotations;

namespace EaglesResort.Models
{
    public class AppSettings
    {
        [Key]
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
