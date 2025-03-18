using System.ComponentModel.DataAnnotations;

namespace ExpenseSplitterAPI.Models
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }  // ✅ Accepts "password" from frontend
    }
}
