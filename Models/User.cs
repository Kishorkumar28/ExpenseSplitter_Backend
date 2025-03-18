using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExpenseSplitterApp.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public List<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
    }
}
