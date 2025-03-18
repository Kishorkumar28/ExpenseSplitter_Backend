using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ExpenseSplitterAPI.Models;

namespace ExpenseSplitterApp.Models
{
    public class Expense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]  // Fix: Set precision
        public decimal Amount { get; set; }

        [Required]
        public int GroupId { get; set; }

        [ForeignKey("GroupId")]
        public Group Group { get; set; }

        [Required]
        public int PaidByUserId { get; set; }

        [ForeignKey("PaidByUserId")]
        public User PaidBy { get; set; }

        public ICollection<ExpenseParticipant> Participants { get; set; } = new List<ExpenseParticipant>();


    }
}
