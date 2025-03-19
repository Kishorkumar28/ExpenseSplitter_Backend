using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ExpenseSplitterApp.Models;
namespace ExpenseSplitterAPI.Models 

{
    public class GroupInvitation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GroupId { get; set; }

        [ForeignKey("GroupId")]
        public Group Group { get; set; } 

        [Required]
        public int InvitedUserId { get; set; }

        [Required]
        public int InvitedByUserId { get; set; }

        [Required]
        public string Status { get; set; } = "Pending"; // "Pending", "Accepted", "Rejected"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
