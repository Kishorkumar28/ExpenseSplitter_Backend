using System.ComponentModel.DataAnnotations;

namespace ExpenseSplitterAPI.Models
{
    public class CreateGroupRequest
    {
        [Required]
        public string Name { get; set; }
    }
}
