using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseSplitterAPI.Services;
using ExpenseSplitterAPI.Models;
using ExpenseSplitterApp.Services;
using Microsoft.EntityFrameworkCore;
using ExpenseSplitterAPI.Data;

namespace ExpenseSplitterAPI.Controllers
{
    [Authorize] // ✅ Requires authentication for all routes
    [Route("api/groups")]
    [ApiController]
    public class GroupsController : ControllerBase
    {
        private readonly GroupService _groupService;
        private readonly ExpenseService _expenseService; // ✅ Inject ExpenseService
        private readonly AppDbContext _context;

        public GroupsController(GroupService groupService, ExpenseService expenseService, AppDbContext context)
        {
            _groupService = groupService;
            _expenseService = expenseService; // ✅ Assign the injected service
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpGet("{groupId}/members")]
        public async Task<IActionResult> GetGroupMembers(int groupId)
        {
            var members = await _context.UserGroups
                .Where(ug => ug.GroupId == groupId)
            .Join(
                    _context.Users,
                    ug => ug.UserId,
                    user => user.UserId,
                    (ug, user) => new { user.UserId, user.Username }
                )
                .ToListAsync();

            if (!members.Any())
            {
                return Ok(new List<object>()); // ✅ Return empty list instead of 404
            }

            return Ok(members);
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
                return Ok(new List<object>());
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

        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetGroupDetails(int groupId) // 🔹 Use int, not Guid
        {
            var group = await _context.Groups
                .Where(g => g.GroupId == groupId) // 🔹 Use GroupId (int)
                .Select(g => new
                {
                    Id = g.GroupId, // 🔹 Ensure correct property name
                    Name = g.Name,
                })
                .FirstOrDefaultAsync();

            if (group == null)
            {
                return NotFound(new { message = "Group not found." });
            }

            return Ok(group);
        }


        // ✅ DTO for debt settlement request
        public class SettleDebtRequest
        {
            public int DebtorId { get; set; }
            public int CreditorId { get; set; }
            public decimal Amount { get; set; }
        }

        [HttpPost("{groupId}/invite")]
        public async Task<IActionResult> InviteUserToGroup(int groupId, [FromBody] InviteUserRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized(new { message = "User ID not found." });

            if (!int.TryParse(userIdClaim.Value, out int invitingUserId)) return Unauthorized(new { message = "Invalid User ID." });

            var success = await _groupService.InviteUserToGroup(invitingUserId, request.InvitedUserId, groupId);

            return success ? Ok(new { message = "User invited successfully!" }) : BadRequest(new { message = "Invitation failed." });
        }

        [HttpGet("invitations")]
        public async Task<IActionResult> GetUserInvitations()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized(new { message = "User ID not found." });

            if (!int.TryParse(userIdClaim.Value, out int userId)) return Unauthorized(new { message = "Invalid User ID." });

            var invitations = await _groupService.GetUserInvitations(userId);

            return Ok(invitations);
        }

        [HttpPost("accept/{inviteId}")]
        public async Task<IActionResult> AcceptInvitation(int inviteId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized(new { message = "User ID not found." });

            if (!int.TryParse(userIdClaim.Value, out int userId)) return Unauthorized(new { message = "Invalid User ID." });

            var success = await _groupService.AcceptInvitation(userId, inviteId);

            return success ? Ok(new { message = "Successfully joined the group!" }) : BadRequest(new { message = "Failed to join the group." });
        }

        [HttpPost("reject/{inviteId}")]
        public async Task<IActionResult> RejectInvitation(int inviteId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized(new { message = "User ID not found." });

            if (!int.TryParse(userIdClaim.Value, out int userId)) return Unauthorized(new { message = "Invalid User ID." });

            var success = await _groupService.RejectInvitation(userId, inviteId);

            return success ? Ok(new { message = "Invitation rejected." }) : BadRequest(new { message = "Failed to reject invitation." });
        }


        // DTO for inviting a user
        public class InviteUserRequest
        {
            public int InvitedUserId { get; set; }
        }


    }

    // ✅ DTO for Creating a Group
    public class CreateGroupRequest
    {
        public string Name { get; set; }
    }
}
