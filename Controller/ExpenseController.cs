using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseSplitterAPI.Services;
using ExpenseSplitterAPI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
namespace ExpenseSplitterAPI.Controllers
{
    [Authorize]
    [Route("api/groups/{groupId}/expenses")]
    [ApiController]
    public class ExpenseController : ControllerBase
    {
        private readonly ExpenseService _expenseService;
        private readonly ExpenseSplitterAPI.Services.WebSocketManager _webSocketManager; // ✅ Explicit reference
        private readonly ILogger<ExpenseController> _logger;

        public ExpenseController(ExpenseService expenseService, ExpenseSplitterAPI.Services.WebSocketManager webSocketManager, ILogger<ExpenseController> logger)
        {
            _expenseService = expenseService;
            _webSocketManager = webSocketManager;
            _logger = logger; // ✅ Inject Logger
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
                return NotFound(new { message = "No expenses found for this group." });
            }

            return Ok(expenses);
        }

        // ✅ Get Group Balances (Fixes 404)
        //[HttpGet("balances")]
        //public async Task<IActionResult> GetGroupBalances(int groupId)
        //{
        //    _logger.LogInformation($"🔍 Fetching balances for group: {groupId}");

        //    var balances = await _expenseService.GetGroupBalancesAsync(groupId);

        //    if (balances == null || balances.Count == 0)
        //    {
        //        _logger.LogWarning($"❌ No balances found for group: {groupId}");
        //        return NotFound(new { message = "No balances found for this group." });
        //    }

        //    _logger.LogInformation($"✅ Returning balances: {balances.Count} records");
        //    return Ok(balances);
        //}

        [HttpPost("settle")]
        public async Task<IActionResult> SettleDebt(int groupId,[FromBody] SettleDebtRequest request)
        {
            if (request == null || request.Amount <= 0 || request.DebtorId <= 0 || request.CreditorId <= 0)
            {
                return BadRequest(new { message = "Invalid settlement request. Check user IDs and amount." });
            }

            bool success = await _expenseService.SettleDebtAsync(request.DebtorId, request.CreditorId, request.Amount, groupId);



            if (success)
            {
                return Ok(new { message = "Debt settled successfully!" });
            }

            return BadRequest(new { message = "No outstanding debt found or settlement failed." });
        }

        // ✅ DTO for debt settlement request
        public class SettleDebtRequest
        {
            public int DebtorId { get; set; }
            public int CreditorId { get; set; }
            public decimal Amount { get; set; }
        }




    }

    // ✅ DTO for Expense Request (Fixes missing reference)
    public class ExpenseRequest
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
    }
}
