using System.ComponentModel.DataAnnotations;

namespace EaglesResort.Models
{
    public class Receptionist
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
