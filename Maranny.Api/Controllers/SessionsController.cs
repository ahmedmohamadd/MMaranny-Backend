using Maranny.Application.DTOs.Sessions;
using Maranny.Application.Features.Sessions.CancelSession;
using Maranny.Application.Features.Sessions.CreateSession;
using Maranny.Application.Features.Sessions.GetAvailableSessions;
using Maranny.Application.Features.Sessions.GetMySessions;
using Maranny.Application.Features.Sessions.UpdateSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/sessions")]
    public class SessionsController : ControllerBase
    {
        private readonly ICreateSessionUseCase _createSessionUseCase;
        private readonly IGetMySessionsUseCase _getMySessionsUseCase;
        private readonly IGetAvailableSessionsUseCase _getAvailableSessionsUseCase;
        private readonly IUpdateSessionUseCase _updateSessionUseCase;
        private readonly ICancelSessionUseCase _cancelSessionUseCase;

        public SessionsController(
            ICreateSessionUseCase createSessionUseCase,
            IGetMySessionsUseCase getMySessionsUseCase,
            IGetAvailableSessionsUseCase getAvailableSessionsUseCase,
            IUpdateSessionUseCase updateSessionUseCase,
            ICancelSessionUseCase cancelSessionUseCase)
        {
            _createSessionUseCase = createSessionUseCase;
            _getMySessionsUseCase = getMySessionsUseCase;
            _getAvailableSessionsUseCase = getAvailableSessionsUseCase;
            _updateSessionUseCase = updateSessionUseCase;
            _cancelSessionUseCase = cancelSessionUseCase;
        }

        [HttpPost]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CreateSession(CreateSessionDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _createSessionUseCase.ExecuteAsync(new CreateSessionCommand(userId, dto));
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpGet("my")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GetMySessions(
            [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _getMySessionsUseCase.ExecuteAsync(new GetMySessionsQuery(userId, status, page, pageSize));
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableSessions(
            [FromQuery] int? coachId, [FromQuery] int? sportId,
            [FromQuery] DateTime? date, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _getAvailableSessionsUseCase.ExecuteAsync(
                new GetAvailableSessionsQuery(coachId, sportId, date, page, pageSize));

            return Ok(result.Value);
        }

        [HttpPut("{sessionId}")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> UpdateSession(int sessionId, UpdateSessionDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _updateSessionUseCase.ExecuteAsync(new UpdateSessionCommand(userId, sessionId, dto));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(new { message = result.Value });
        }

        [HttpDelete("{sessionId}")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CancelSession(int sessionId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _cancelSessionUseCase.ExecuteAsync(new CancelSessionCommand(userId, sessionId));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(new { message = result.Value });
        }
    }
}
