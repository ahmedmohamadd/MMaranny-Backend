using Maranny.Application.DTOs.Auth;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            var (success, message, data) = await _authService.RegisterAsync(
                dto, Request.Scheme, Request.Host.ToString());
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message, user = data });
        }

        [HttpPost("coach-onboarding/complete")]
        [AllowAnonymous]
        public async Task<IActionResult> CompleteCoachOnboarding(CompleteCoachOnboardingDto dto)
        {
            var (success, message, data) = await _authService.CompleteCoachOnboardingAsync(dto);
            if (message == "AccountBlocked") return StatusCode(403, new { error = message });
            if (!success) return BadRequest(new { error = message, data });
            return Ok(new { message, data });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var (success, statusCode, message, data) = await _authService.LoginAsync(dto);
            if (!success) return StatusCode(statusCode, new { error = message });
            return Ok(data);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh(RefreshTokenDto dto)
        {
            var (success, statusCode, message, data) = await _authService.RefreshTokenAsync(dto);
            if (!success) return StatusCode(statusCode, new { error = message });
            return Ok(data);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] LogoutDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _authService.LogoutAsync(userId, dto);
            return Ok(new { message });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _authService.GetCurrentUserAsync(userId);
            if (!success) return NotFound(new { error = message });
            return Ok(data);
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            var (_, message) = await _authService.ForgotPasswordAsync(dto);
            return Ok(new { message });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            var (success, message) = await _authService.ResetPasswordAsync(dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpPut("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _authService.ChangePasswordAsync(userId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpGet("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(int userId, string token)
        {
            if (string.IsNullOrEmpty(token)) return BadRequest(new { error = "Invalid confirmation token" });

            var (success, message) = await _authService.ConfirmEmailAsync(userId, token);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpPost("resend-confirmation")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendConfirmationDto dto)
        {
            var (success, message) = await _authService.ResendConfirmationAsync(
                dto, Request.Scheme, Request.Host.ToString());
            if (!success) return StatusCode(500, new { error = message });
            return Ok(new { message });
        }

        [HttpPost("google-login")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLogin(GoogleLoginDto dto)
        {
            var (success, statusCode, message, data) = await _authService.GoogleLoginAsync(dto);
            if (!success) return StatusCode(statusCode, new { error = message });
            return Ok(data);
        }
    }
}