using Maranny.Application.DTOs.Admin;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        [HttpPost("users/{userId}/block")]
        public async Task<IActionResult> BlockUser(int userId, [FromBody] BlockUserDto dto)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(adminIdClaim, out int adminId)) return Unauthorized();

            var (success, message) = await _adminService.BlockUserAsync(adminId, userId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpPost("users/{userId}/unblock")]
        public async Task<IActionResult> UnblockUser(int userId)
        {
            var (success, message) = await _adminService.UnblockUserAsync(userId);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpGet("coaches/pending")]
        public async Task<IActionResult> GetPendingCoaches()
        {
            var result = await _adminService.GetPendingCoachesAsync();
            return Ok(result);
        }

        [HttpPost("coaches/{coachId}/verify")]
        public async Task<IActionResult> VerifyCoach(int coachId, [FromBody] VerifyCoachDto dto)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(adminIdClaim, out int adminId)) return Unauthorized();

            var (success, message) = await _adminService.VerifyCoachAsync(adminId, coachId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpPost("coaches/{coachId}/reject")]
        public async Task<IActionResult> RejectCoach(int coachId, [FromBody] RejectCoachDto dto)
        {
            var (success, message) = await _adminService.RejectCoachAsync(coachId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUserDetails(int userId)
        {
            var (success, data) = await _adminService.GetUserDetailsAsync(userId);
            if (!success) return NotFound(new { error = "User not found" });
            return Ok(data);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? role, [FromQuery] bool? isBlocked,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _adminService.GetUsersAsync(role, isBlocked, page, pageSize);
            return Ok(result);
        }

        [HttpGet("certificates/pending")]
        public async Task<IActionResult> GetPendingCertificates()
        {
            var result = await _adminService.GetPendingCertificatesAsync();
            return Ok(result);
        }

        [HttpPut("certificates/{coachId}/verify")]
        public async Task<IActionResult> VerifyCertificate(int coachId, [FromBody] string? notes = null)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(adminIdClaim, out int adminId)) return Unauthorized();

            var (success, message) = await _adminService.VerifyCertificateAsync(adminId, coachId, notes);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpGet("reviews/pending")]
        public async Task<IActionResult> GetPendingReviews(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _adminService.GetPendingReviewsAsync(page, pageSize);
            return Ok(result);
        }

        [HttpPut("reviews/{reviewId}/moderate")]
        public async Task<IActionResult> ModerateReview(int reviewId, [FromBody] string action = "delete")
        {
            var (success, message) = await _adminService.ModerateReviewAsync(reviewId, action);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
        {
            var result = await _adminService.GetAnalyticsAsync();
            return Ok(result);
        }
    }
}