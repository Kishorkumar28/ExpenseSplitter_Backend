using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ExpenseSplitterAPI.Data;
using ExpenseSplitterAPI.Models;
using ExpenseSplitterAPI.Services;
using ExpenseSplitterApp.Models;

namespace ExpenseSplitterAPI.Services
{
    public class ExpenseService
    {
        private readonly AppDbContext _context;
        private readonly WebSocketManager _webSocketManager;

        public ExpenseService(AppDbContext context, WebSocketManager webSocketManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
        }

        // ✅ Fetch Balance Between Two Users in a Group
        public async Task<Debt> GetBalanceBetweenUsersAsync(int debtorId, int creditorId, int groupId)
        {
            return await _context.Debts
                .FirstOrDefaultAsync(d => d.OwedByUserId == debtorId && d.OwedToUserId == creditorId && d.GroupId == groupId);
        }

        private async Task RecalculateBalancesAsync(int groupId)
        {
            Console.WriteLine($"🔄 Recalculating balances for Group {groupId}...");

            // ✅ Instead of deleting all debts, only delete debts that are fully paid (Amount <= 0)
            await _context.Debts
                .Where(d => d.GroupId == groupId && d.Amount <= 0)
                .ExecuteDeleteAsync(); // ✅ Deletes only fully settled debts

            Console.WriteLine($"✅ Balances recalculated successfully for Group {groupId}!");
        }


        // ✅ Add Expense (With WebSocket Notification)
        public async Task<Expense> AddExpenseAsync(int groupId, int userId, string description, decimal amount)
        {
            var groupExists = await _context.Groups.AnyAsync(g => g.GroupId == groupId);
            if (!groupExists)
                throw new Exception($"Group with ID {groupId} does not exist.");

            var expense = new Expense
            {
                GroupId = groupId,
                PaidByUserId = userId,
                Description = description,
                Amount = amount
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            var participants = await _context.UserGroups
                .Where(ug => ug.GroupId == groupId)
                .Select(ug => ug.UserId)
                .ToListAsync();

            if (participants.Count <= 1)
            {
                Console.WriteLine($"⚠️ Not enough users in Group {groupId} to split the expense.");
                return expense;
            }

            decimal share = amount / participants.Count;

            foreach (var participant in participants)
            {
                if (participant == userId) continue;

                var existingDebt = await _context.Debts
                    .FirstOrDefaultAsync(d => d.OwedByUserId == participant && d.OwedToUserId == userId && d.GroupId == groupId);

                if (existingDebt != null)
                {
                    // ✅ Update the existing debt amount instead of creating a duplicate
                    existingDebt.Amount += share;
                }
                else
                {
                    // ✅ Insert new debt only if it doesn’t already exist
                    _context.Debts.Add(new Debt
                    {
                        OwedByUserId = participant,
                        OwedToUserId = userId,
                        Amount = share,
                        GroupId = groupId
                    });
                }
            }

            await _context.SaveChangesAsync();

            Console.WriteLine($"✅ Expense added and balances updated for Group {groupId}.");

            return expense;
        }




        // ✅ Get Group Expenses
        public async Task<List<object>> GetExpensesByGroupIdAsync(int groupId)
        {
            var expenses = await _context.Expenses
                .Where(e => e.GroupId == groupId)
                .Include(e => e.PaidBy)
                .Include(e => e.Participants)
                .ThenInclude(p => p.User)
                .ToListAsync();

            return expenses.Select(expense => new
            {
                Id = expense.Id,
                Description = expense.Description,
                Amount = expense.Amount,
                GroupId = expense.GroupId,
                PaidByUserId = expense.PaidByUserId,
                PaidByUsername = expense.PaidBy?.Username ?? "Unknown",
                Participants = expense.Participants.Select(p => new
                {
                    UserId = p.UserId,
                    Username = p.User?.Username ?? "Unknown"
                }).ToList()
            }).ToList<object>();
        }

        // ✅ Get Group Balances
        public async Task<List<object>> GetGroupBalancesAsync(int groupId)
        {
            var debts = await _context.Debts
                .Where(d => d.GroupId == groupId)
                .Join(
                    _context.Users,
                    debt => debt.OwedByUserId,
                    user => user.UserId,
                    (debt, debtor) => new { debt, DebtorName = debtor.Username }
                )
                .Join(
                    _context.Users,
                    combined => combined.debt.OwedToUserId,
                    user => user.UserId,
                    (combined, creditor) => new
                    {
                        DebtorId = combined.debt.OwedByUserId,
                        DebtorName = combined.DebtorName ?? "Unknown", // ✅ Ensures username is not null
                        CreditorId = combined.debt.OwedToUserId,
                        CreditorName = creditor.Username ?? "Unknown", // ✅ Ensures username is not null
                        Amount = combined.debt.Amount // ✅ No need for `??` because Amount is already decimal
                    }
                )
                .ToListAsync();

            if (!debts.Any())
            {
                Console.WriteLine($"❌ No balances found for Group {groupId}.");
                return new List<object>(); // ✅ Returns empty list instead of null
            }

            Console.WriteLine($"✅ Found {debts.Count} balances for Group {groupId}.");

            return debts.Select(d => (object)new
            {
                DebtorId = d.DebtorId,
                DebtorName = d.DebtorName,
                CreditorId = d.CreditorId,
                CreditorName = d.CreditorName,
                Amount = d.Amount
            }).ToList();
        }







        // ✅ Settle Debt Between Two Users
        public async Task<bool> SettleDebtAsync(int debtorId, int creditorId, decimal amount, int groupId)
        {
            var existingDebt = await GetBalanceBetweenUsersAsync(debtorId, creditorId, groupId);

            if (existingDebt == null || existingDebt.Amount < amount)
            {
                Console.WriteLine($"❌ Settlement failed: No sufficient debt between {debtorId} and {creditorId}");
                return false;
            }

            Console.WriteLine($"💰 Settling ₹{amount} from User {debtorId} to User {creditorId}. Existing Debt: ₹{existingDebt.Amount}");

            if (amount >= existingDebt.Amount)
            {
                _context.Debts.Remove(existingDebt);
                Console.WriteLine($"✅ Full debt of ₹{existingDebt.Amount} settled. Removing debt entry.");

                if (amount > existingDebt.Amount)
                {
                    decimal overpaidAmount = amount - existingDebt.Amount;
                    await _context.Debts.AddAsync(new Debt
                    {
                        OwedByUserId = creditorId,
                        OwedToUserId = debtorId,
                        Amount = overpaidAmount,
                        GroupId = groupId
                    });
                    Console.WriteLine($"✅ Overpayment of ₹{overpaidAmount}. Now User {creditorId} owes User {debtorId}.");
                }
            }
            else
            {
                existingDebt.Amount -= amount;
                _context.Debts.Update(existingDebt);
                Console.WriteLine($"⚖️ Partial settlement. ₹{existingDebt.Amount} still owed after payment.");
            }

            await _context.SaveChangesAsync();
            await RecalculateBalancesAsync(groupId);
            await _webSocketManager.BroadcastAsync($"balance_updated:{groupId}");

            return true;
        }

        // ✅ WebSocket Notification Helper
        private async Task NotifyClients(string message)
        {
            try
            {
                await _webSocketManager.BroadcastAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ WebSocket Broadcast Error: {ex.Message}");
            }
        }
    }
}
