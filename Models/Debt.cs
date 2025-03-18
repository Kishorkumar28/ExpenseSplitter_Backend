using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExpenseSplitterApp.Models
{
    public class Debt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OwedByUserId { get; set; }

        [ForeignKey("OwedByUserId")]
        public User OwedBy { get; set; }

        [Required]
        public int OwedToUserId { get; set; }

        [ForeignKey("OwedToUserId")]
        public User OwedTo { get; set; }

        [Required]
        public int GroupId { get; set; }

        [ForeignKey("GroupId")]
        public Group Group { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public bool IsSettled { get; set; } = false;
    }
}
