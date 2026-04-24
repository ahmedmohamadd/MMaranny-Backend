using Maranny.Application.DTOs.Bookings;
using Maranny.Application.DTOs.Sessions;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/bookings")]
    [Authorize]
    public class BookingsController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingsController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> BookSession(CreateBookingDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _bookingService.BookSessionAsync(userId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(data);
        }

        [HttpGet("my")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetMyBookings(
            [FromQuery] string? status, [FromQuery] string? tab,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, data) = await _bookingService.GetMyBookingsAsync(userId, status, tab, page, pageSize);
            if (!success) return NotFound(new { error = "Client profile not found" });
            return Ok(data);
        }

        [HttpGet("{bookingId:int}")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetBookingDetails(int bookingId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _bookingService.GetBookingDetailsAsync(userId, bookingId);
            if (message == "Forbidden") return Forbid();
            if (!success) return NotFound(new { error = message });
            return Ok(data);
        }

        [HttpGet("coach/my")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GetCoachBookings(
            [FromQuery] string? status, [FromQuery] string? tab,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, data) = await _bookingService.GetCoachBookingsAsync(userId, status, tab, page, pageSize);
            if (!success) return NotFound(new { error = "Coach profile not found" });
            return Ok(data);
        }

        [HttpPut("{bookingId}/approve")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> ApproveBooking(int bookingId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _bookingService.ApproveBookingAsync(userId, bookingId);
            if (message == "Forbidden") return Forbid();
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpPut("{bookingId}/decline")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> DeclineBooking(int bookingId, [FromBody] CoachBookingActionDto? dto = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _bookingService.DeclineBookingAsync(userId, bookingId, dto);
            if (message == "Forbidden") return Forbid();
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message });
        }

        [HttpPut("{bookingId}/cancel")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> CancelBooking(int bookingId, [FromQuery] string? reason = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _bookingService.CancelBookingAsync(userId, bookingId, reason);
            if (message == "Forbidden") return Forbid();
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message, data });
        }

        [HttpPut("session/{sessionId}/cancel-by-coach")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CoachCancelSession(int sessionId, [FromQuery] string? reason = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _bookingService.CoachCancelSessionAsync(userId, sessionId, reason);
            if (message == "Forbidden") return Forbid();
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message, data });
        }
    }
}