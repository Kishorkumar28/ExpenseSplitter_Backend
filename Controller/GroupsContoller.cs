using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseSplitterAPI.Services;
using ExpenseSplitterAPI.Models;
using ExpenseSplitterApp.Services;

namespace ExpenseSplitterAPI.Controllers
{
    [Authorize] // ✅ Requires authentication for all routes
    [Route("api/groups")]
    [ApiController]
    public class GroupsController : ControllerBase
    {
        private readonly GroupService _groupService;
        private readonly ExpenseService _expenseService; // ✅ Inject ExpenseService

        public GroupsController(GroupService groupService, ExpenseService expenseService)
        {
            _groupService = groupService;
            _expenseService = expenseService; // ✅ Assign the injected service
        }

        // ✅ Create Group (Proper DTO)
        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Group name is required." });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "User ID not found in token." });
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid User ID format in token." });
            }

            var group = await _groupService.CreateGroupAsync(request.Name, userId);
            return Ok(group);
        }

        // ✅ Join Group
        [HttpPost("join/{groupId}")]
        public async Task<IActionResult> JoinGroup(int groupId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "User ID not found in token." });
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid User ID format in token." });
            }

            var success = await _groupService.JoinGroupAsync(userId, groupId);

            return success ? Ok(new { message = "Joined group successfully!" }) : BadRequest(new { message = "Already in the group or group doesn't exist." });
        }

        // ✅ Get User's Groups
        [HttpGet]
        public async Task<IActionResult> GetUserGroups()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "User ID not found in token." });
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "Invalid User ID format in token." });
            }

            var groups = await _groupService.GetUserGroupsAsync(userId);
            return Ok(groups.ToList()); // ✅ Ensures proper JSON format
        }

        // ✅ Get All Groups
        [HttpGet("all")]
        public async Task<IActionResult> GetAllGroups()
        {
            var groups = await _groupService.GetAllGroupsAsync();
            return Ok(groups);
        }

        // ✅ Fetch balances at group level (Fixed Route)
        [HttpGet("{groupId}/balances")]
        public async Task<IActionResult> GetGroupBalances(int groupId)
        {
            var balances = await _expenseService.GetGroupBalancesAsync(groupId);

            if (balances == null || balances.Count == 0)
            {
                return NotFound(new { message = "No balances found for this group." });
            }

            return Ok(balances);
        }

        [HttpPost("{groupId}/settle")]
        public async Task<IActionResult> SettleDebt(int groupId, [FromBody] SettleDebtRequest request)
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

    // ✅ DTO for Creating a Group
    public class CreateGroupRequest
    {
        public string Name { get; set; }
    }
}
