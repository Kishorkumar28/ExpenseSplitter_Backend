using ExpenseSplitterAPI.Models;
using ExpenseSplitterAPI.Services;
using Microsoft.AspNetCore.Mvc;
using ExpenseSplitterApp.Models;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.Data;

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

        // ✅ FIXED: Use RegisterRequest DTO instead of User model
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] ExpenseSplitterAPI.Models.RegisterRequest request)
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

        // ✅ FIXED: Use LoginRequest DTO & password verification
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Microsoft.AspNetCore.Identity.Data.LoginRequest request)
        {
            var token = await _authService.Login(request.Email, request.Password);
            if (token == null)
                return Unauthorized(new { message = "Invalid credentials" });

            return Ok(new { token });
        }
    }
}
