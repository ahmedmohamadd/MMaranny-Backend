using Maranny.Application.DTOs.Profile;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _userService.UpdateProfileAsync(userId, dto);
            if (!success) return NotFound(new { error = message });
            return Ok(new { message });
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences(UpdatePreferencesDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _userService.UpdatePreferencesAsync(userId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message, savedPreferences = data });
        }

        [HttpGet("coach-setup")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GetCoachSetup()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, data) = await _userService.GetCoachSetupAsync(userId);
            if (!success) return NotFound(new { error = "Coach profile not found" });
            return Ok(data);
        }

        [HttpPut("coach-setup")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> UpdateCoachSetup(UpdateCoachSetupDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _userService.UpdateCoachSetupAsync(userId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpPost("profile/image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProfileImage([FromForm] UploadImageDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            using var stream = dto.File.OpenReadStream();
            var (success, message, data) = await _userService.UploadProfileImageAsync(
                userId, stream, dto.File.FileName, dto.File.Length);

            if (!success) return BadRequest(new { error = message });
            return Ok(new { message, data });
        }
    }

    public class UploadImageDto
    {
        public IFormFile File { get; set; } = null!;
    }
}