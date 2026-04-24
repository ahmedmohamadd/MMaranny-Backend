using Maranny.Application.DTOs.Sessions;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/sessions")]
    public class SessionsController : ControllerBase
    {
        private readonly ISessionService _sessionService;

        public SessionsController(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        [HttpPost]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CreateSession(CreateSessionDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _sessionService.CreateSessionAsync(userId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message, data });
        }

        [HttpGet("my")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GetMySessions(
            [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, data) = await _sessionService.GetMySessionsAsync(userId, status, page, pageSize);
            if (!success) return NotFound(new { error = "Coach profile not found" });
            return Ok(data);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableSessions(
            [FromQuery] int? coachId, [FromQuery] int? sportId,
            [FromQuery] DateTime? date, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _sessionService.GetAvailableSessionsAsync(coachId, sportId, date, page, pageSize);
            return Ok(result);
        }

        [HttpPut("{sessionId}")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> UpdateSession(int sessionId, UpdateSessionDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _sessionService.UpdateSessionAsync(userId, sessionId, dto);
            if (message == "Forbidden") return Forbid();
            if (!success) return NotFound(new { error = message });
            return Ok(new { message });
        }

        [HttpDelete("{sessionId}")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CancelSession(int sessionId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _sessionService.CancelSessionAsync(userId, sessionId);
            if (message == "Forbidden") return Forbid();
            if (!success) return NotFound(new { error = message });
            return Ok(new { message });
        }
    }
}