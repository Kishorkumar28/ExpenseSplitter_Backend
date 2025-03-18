using ExpenseSplitterAPI.Models;
using ExpenseSplitterAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ExpenseSplitterApp.Models;

namespace ExpenseSplitterAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        // ✅ Register a New User
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _authService.UserExists(request.Email))
            {
                return BadRequest(new { message = "Email is already in use." });
            }

            var user = new User
            {
                Username = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            var createdUser = await _authService.Register(user);
            return Ok(new { message = "User registered successfully", createdUser });
        }

        // ✅ User Login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var token = await _authService.Login(request.Email, request.Password);
            if (token == null)
                return Unauthorized(new { message = "Invalid credentials" });

            return Ok(new { token });
        }

        // ✅ Change Password
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (request.NewPassword != request.ConfirmPassword)
            {
                return BadRequest(new { message = "New passwords do not match." });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _authService.GetUserById(int.Parse(userId));

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Current password is incorrect." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _authService.UpdateUser(user);

            return Ok(new { message = "Password updated successfully!" });
        }

        // ✅ Change Username
        [Authorize]
        [HttpPost("change-username")]
        public async Task<IActionResult> ChangeUsername([FromBody] ChangeUsernameRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _authService.GetUserById(int.Parse(userId));

            if (user == null)
            {
                return BadRequest(new { message = "User not found." });
            }

            user.Username = request.NewUsername;
            await _authService.UpdateUser(user);

            return Ok(new { message = "Username updated successfully!" });
        }

        // ✅ Get User Profile
        [Authorize]
        [HttpGet("user")]
        public async Task<IActionResult> GetUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
            {
                return Unauthorized(new { message = "Invalid token. Please log in again." });
            }

            var user = await _authService.GetUserById(int.Parse(userId));

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            return Ok(new
            {
                userId = user.UserId,
                username = user.Username,
                email = user.Email
            });
        }

    }

    // ✅ DTOs (Request Models)
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class ChangeUsernameRequest
    {
        public string NewUsername { get; set; }
    }
}
