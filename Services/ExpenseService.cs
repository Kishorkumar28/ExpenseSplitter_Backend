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
        private readonly WebSocketManager _webSocketManager; // ✅ WebSocket Manager

        public ExpenseService(AppDbContext context, WebSocketManager webSocketManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager));
        }

        private async Task RecalculateBalancesAsync(int groupId)
        {
            Console.WriteLine($"🔄 Recalculating balances for group {groupId}...");

            var updatedBalances = await GetGroupBalancesAsync(groupId); // 🔥 Ensure fresh balance update
            Console.WriteLine($"✅ Updated balances after settlement: {updatedBalances.Count} records.");

            // 🔥 Send real-time update to the frontend
            await _webSocketManager.BroadcastAsync($"balance_updated:{groupId}");
        }




        // ✅ Add Expense to a Group (With WebSocket Notification)
        public async Task<Expense> AddExpenseAsync(int groupId, int userId, string description, decimal amount)
        {
            // ✅ Ensure the group exists
            var groupExists = await _context.Groups.AnyAsync(g => g.GroupId == groupId);
            if (!groupExists)
                throw new Exception($"Group with ID {groupId} does not exist."); // Better debugging

            var expense = new Expense
            {
                GroupId = groupId,
                PaidByUserId = userId,
                Description = description,
                Amount = amount
            };

            // ✅ Save expense first
            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync(); // Ensure expense ID is generated

            // ✅ Fetch all group members
            var groupMembers = await _context.UserGroups
                .Where(ug => ug.GroupId == groupId)
                .Select(ug => ug.UserId)
                .ToListAsync();

            // ✅ Insert expense participants safely
            foreach (var memberId in groupMembers)
            {
                if (!await _context.Users.AnyAsync(u => u.UserId == memberId)) // Check user exists
                {
                    Console.WriteLine($"⚠️ Skipping non-existent UserId: {memberId}");
                    continue;
                }

                _context.ExpenseParticipants.Add(new ExpenseParticipant
                {
                    ExpenseId = expense.Id, // Now guaranteed to be valid
                    UserId = memberId
                });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"❌ DbUpdateException: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"🔍 Inner Exception: {ex.InnerException.Message}");
                }
                throw; // Re-throw the exception for further debugging
            }

            // ✅ **Trigger balance update after expense is added**
            Console.WriteLine($"🔄 Recalculating balances for group {groupId}");
            await RecalculateBalancesAsync(groupId); // 🔥 **Recalculate balances after adding an expense**

            // ✅ **WebSocket Notification**
            await _webSocketManager.BroadcastAsync($"new_expense:{groupId}");
            await _webSocketManager.BroadcastAsync($"balance_updated:{groupId}");

            return expense;
        }









        // ✅ Get All Expenses by Group ID (Optimized)
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

        // ✅ Get Group Balances (With Better Formatting)
        public async Task<Dictionary<string, Dictionary<string, decimal>>> GetGroupBalancesAsync(int groupId)
        {
            var expenses = await _context.Expenses
                .Where(e => e.GroupId == groupId)
                .Include(e => e.PaidBy)
                .Include(e => e.Participants)
                .ThenInclude(ep => ep.User)
                .ToListAsync();

            var userBalances = new Dictionary<string, decimal>();

            // ✅ Step 1: Calculate total paid by each user
            foreach (var expense in expenses)
            {
                string payerName = expense.PaidBy?.Username ?? "Unknown";

                if (!userBalances.ContainsKey(payerName))
                    userBalances[payerName] = 0;

                userBalances[payerName] += expense.Amount;
            }

            // ✅ Step 2: Calculate fair share per user
            int totalUsers = userBalances.Count;
            decimal totalAmount = userBalances.Values.Sum();
            decimal fairShare = totalUsers > 0 ? totalAmount / totalUsers : 0;

            foreach (var user in userBalances.Keys.ToList())
            {
                userBalances[user] -= fairShare;
            }

            // ✅ Step 3: Compute debts & store in database
            var finalBalances = new Dictionary<string, Dictionary<string, decimal>>();
            var debtors = userBalances.Where(x => x.Value < 0).OrderBy(x => x.Value).ToList();
            var creditors = userBalances.Where(x => x.Value > 0).OrderByDescending(x => x.Value).ToList();

            var newDebts = new List<Debt>();

            int i = 0, j = 0;
            while (i < debtors.Count && j < creditors.Count)
            {
                string debtor = debtors[i].Key;
                string creditor = creditors[j].Key;
                decimal amount = Math.Min(-debtors[i].Value, creditors[j].Value);

                if (!finalBalances.ContainsKey(debtor))
                    finalBalances[debtor] = new Dictionary<string, decimal>();

                finalBalances[debtor][creditor] = amount;

                var debtorUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == debtor);
                var creditorUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == creditor);

                if (debtorUser != null && creditorUser != null)
                {
                    newDebts.Add(new Debt
                    {
                        OwedByUserId = debtorUser.UserId,
                        OwedToUserId = creditorUser.UserId,
                        Amount = amount,
                        GroupId = groupId
                    });
                }

                debtors[i] = new KeyValuePair<string, decimal>(debtor, debtors[i].Value + amount);
                creditors[j] = new KeyValuePair<string, decimal>(creditor, creditors[j].Value - amount);

                if (Math.Abs(debtors[i].Value) < 0.01m) i++;
                if (Math.Abs(creditors[j].Value) < 0.01m) j++;
            }

            await _context.Database.ExecuteSqlRawAsync($"DELETE FROM Debts WHERE GroupId = {groupId}");
            await _context.Debts.AddRangeAsync(newDebts); // ✅ Save debts in one go
            await _context.SaveChangesAsync(); // ✅ Save once

            return finalBalances;
        }




        // ✅ Settle Debt (With WebSocket Notification)
        public async Task<bool> SettleDebtAsync(int debtorId, int creditorId, decimal amount, int groupId)
        {
            var existingDebt = await _context.Debts
                .FirstOrDefaultAsync(d => d.OwedByUserId == debtorId && d.OwedToUserId == creditorId && d.GroupId == groupId);

            if (existingDebt == null)
            {
                Console.WriteLine($"❌ No outstanding debt found between {debtorId} and {creditorId}");
                return false; // No debt to settle
            }

            Console.WriteLine($"💰 Settling ₹{amount} from User {debtorId} to User {creditorId}. Existing Debt: ₹{existingDebt.Amount}");

            if (amount >= existingDebt.Amount)
            {
                await _context.Database.ExecuteSqlRawAsync($"DELETE FROM Debts WHERE OwedByUserId = {debtorId} AND OwedToUserId = {creditorId} AND GroupId = {groupId}");
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

            await _context.SaveChangesAsync(); // ✅ Save only once at the end

            // ✅ **Ensure frontend updates**
            await RecalculateBalancesAsync(groupId); // 🔥 Force refresh balances after settlement

            await _webSocketManager.BroadcastAsync($"balance_updated:{groupId}"); // ✅ WebSocket notification
            return true;
        }




        // ✅ WebSocket Broadcast Helper
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
