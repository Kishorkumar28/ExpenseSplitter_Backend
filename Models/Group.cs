using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ExpenseSplitterAPI.Models;

namespace ExpenseSplitterApp.Models
{
    public class Group
    {
        [Key]
        public int GroupId { get; set; }

        [Required]
        public string Name { get; set; }

        [JsonIgnore] // ✅ Prevents infinite recursion
        public ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();

        [JsonIgnore] // ✅ Prevents infinite recursion
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}
