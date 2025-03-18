using System.Text.Json.Serialization;
using ExpenseSplitterAPI.Models;

namespace ExpenseSplitterApp.Models
{
    public class UserGroup
    {
        public int UserId { get; set; }

        [JsonIgnore] // ✅ Prevents infinite recursion
        public User User { get; set; }

        public int GroupId { get; set; }

        [JsonIgnore] // ✅ Prevents infinite recursion
        public Group Group { get; set; }
    }
}
