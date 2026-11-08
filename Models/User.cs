using System.ComponentModel.DataAnnotations;

namespace EaglesResort.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = "Admin"; // Admin, Receptionist

        public string FullName { get; set; } = string.Empty;
    }
}
