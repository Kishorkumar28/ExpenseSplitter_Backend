using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseSplitterAPI.Services;
using ExpenseSplitterAPI.Models;
using Microsoft.Extensions.Logging;

namespace ExpenseSplitterAPI.Controllers
{
    [Authorize]
    [Route("api/groups/{groupId}/expenses")]
    [ApiController]
    public class ExpenseController : ControllerBase
    {
        private readonly ExpenseService _expenseService;
        private readonly ExpenseSplitterAPI.Services.WebSocketManager _webSocketManager;
        private readonly ILogger<ExpenseController> _logger;

        public ExpenseController(ExpenseService expenseService, ExpenseSplitterAPI.Services.WebSocketManager webSocketManager, ILogger<ExpenseController> logger)
        {
            _expenseService = expenseService ?? throw new ArgumentNullException(nameof(expenseService));
            _webSocketManager = webSocketManager ?? throw new ArgumentNullException(nameof(webSocketManager)); // ✅ Correctly assign the parameter
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        // ✅ Add Expense to a Group (Triggers WebSocket)
        [HttpPost]
        public async Task<IActionResult> AddExpense(int groupId, [FromBody] ExpenseRequest request)
        {
            if (request == null || request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { message = "Invalid expense details." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "User ID not found in token." });
            }

            var userId = int.Parse(userIdClaim.Value);

            var expense = await _expenseService.AddExpenseAsync(groupId, userId, request.Description, request.Amount);

            if (expense == null)
            {
                return BadRequest(new { message = "Failed to add expense. Ensure the group exists." });
            }

            // 🔹 Notify all WebSocket clients
            await _webSocketManager.BroadcastAsync($"new_expense:{groupId}");
            await _webSocketManager.BroadcastAsync($"balance_updated:{groupId}");

            return Ok(new { message = "Expense added successfully!", expense });
        }

        // ✅ Get All Expenses for a Group
        [HttpGet]
        public async Task<IActionResult> GetExpenses(int groupId)
        {
            var expenses = await _expenseService.GetExpensesByGroupIdAsync(groupId);

            if (expenses == null || expenses.Count == 0)
            {
                return Ok(new List<ExpenseDto>());
            }

            return Ok(expenses);
        }

        // ✅ Get Group Balances
        [HttpGet("balances")]
        public async Task<IActionResult> GetGroupBalances(int groupId)
        {
            _logger.LogInformation($"🔍 Fetching balances for group: {groupId}");

            var balances = await _expenseService.GetGroupBalancesAsync(groupId);

            if (balances == null || balances.Count == 0)
            {
                Console.WriteLine($"✅ No outstanding debts for Group {groupId}");
                return Ok(new List<object>()); // ✅ Return empty list with 200 OK instead of 404
            }

            _logger.LogInformation($"✅ Returning balances: {balances.Count} records");
            return Ok(balances);
        }

        // ✅ Settle Debt between two users in a group
        [HttpPost("settle")]
        public async Task<IActionResult> SettleDebt(int groupId, [FromBody] SettleDebtRequest request)
        {
            if (request == null || request.Amount <= 0 || request.DebtorId <= 0 || request.CreditorId <= 0)
            {
                return BadRequest(new { message = "Invalid settlement request. Check user IDs and amount." });
            }

            _logger.LogInformation($"🔄 Attempting to settle debt: {request.DebtorId} → {request.CreditorId}, Amount: {request.Amount}");

            var balance = await _expenseService.GetBalanceBetweenUsersAsync(request.DebtorId, request.CreditorId, groupId);

            if (balance == null || balance.Amount < request.Amount)
            {
                _logger.LogWarning($"❌ No outstanding debt found or insufficient balance for {request.DebtorId} → {request.CreditorId}");
                return BadRequest(new { message = "No outstanding debt found or settlement failed." });
            }

            bool success = await _expenseService.SettleDebtAsync(request.DebtorId, request.CreditorId, request.Amount, groupId);

            if (success)
            {
                // 🔹 Notify WebSocket clients of balance update
                await _webSocketManager.BroadcastAsync($"balance_updated:{groupId}");
                _logger.LogInformation($"✅ Debt settled successfully: {request.DebtorId} → {request.CreditorId}");
                return Ok(new { message = "Debt settled successfully!" });
            }

            return BadRequest(new { message = "Settlement failed due to server error." });
        }

        // ✅ DTO for debt settlement request
        public class SettleDebtRequest
        {
            public int DebtorId { get; set; }
            public int CreditorId { get; set; }
            public decimal Amount { get; set; }
        }

        // ✅ DTO for Expense Request
        public class ExpenseRequest
        {
            public string Description { get; set; }
            public decimal Amount { get; set; }
        }
    }
}
