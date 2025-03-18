using System.ComponentModel.DataAnnotations;

namespace ExpenseSplitterAPI.Models
{
    public class RegisterRequest
    {
        [Required]
        public string Name { get; set; }  // ✅ Accepts "name" from frontend

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }  // ✅ Accepts "password" from frontend
    }
}
