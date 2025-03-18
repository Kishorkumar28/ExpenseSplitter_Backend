using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ExpenseSplitterApp.Models;

namespace ExpenseSplitterAPI.Models
{
    public class ExpenseParticipant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // ✅ Ensures Auto-Increment
        public int Id { get; set; }

        [ForeignKey("Expense")]
        public int ExpenseId { get; set; }
        public virtual Expense Expense { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }
    }
}
