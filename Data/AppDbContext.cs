using Microsoft.EntityFrameworkCore;
using ExpenseSplitterApp.Models;
using ExpenseSplitterAPI.Models;
using ExpenseSplitterAPI.Services;

namespace ExpenseSplitterAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<UserGroup> UserGroups { get; set; }
        public DbSet<Debt> Debts { get; set; }
        public DbSet<ExpenseParticipant> ExpenseParticipants { get; set; }

        public DbSet<GroupInvitation> GroupInvitations { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ✅ FIX: Restrict delete behavior for PaidByUserId in Expenses
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.PaidBy)
                .WithMany()
                .HasForeignKey(e => e.PaidByUserId)
                .OnDelete(DeleteBehavior.Restrict);  // ❌ Prevents multiple cascade paths

            // ✅ FIX: Prevent multiple cascade paths in Debts
            modelBuilder.Entity<Debt>()
                .HasOne(d => d.OwedBy)
                .WithMany()
                .HasForeignKey(d => d.OwedByUserId)
                .OnDelete(DeleteBehavior.Cascade); // ✅ Allows cascade delete

            modelBuilder.Entity<Debt>()
                .HasOne(d => d.OwedTo)
                .WithMany()
                .HasForeignKey(d => d.OwedToUserId)
                .OnDelete(DeleteBehavior.Restrict); // ❌ Prevents multiple cascade paths

            modelBuilder.Entity<UserGroup>()
                .HasKey(ug => new { ug.UserId, ug.GroupId });

            modelBuilder.Entity<ExpenseParticipant>()
                .HasKey(ep => new { ep.ExpenseId, ep.UserId });

            modelBuilder.Entity<Expense>()
                .Property(e => e.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Debt>()
                .Property(d => d.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<GroupInvitation>()
        .HasKey(i => i.Id);

            modelBuilder.Entity<GroupInvitation>()
                .HasOne(i => i.Group)
                .WithMany()
                .HasForeignKey(i => i.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
