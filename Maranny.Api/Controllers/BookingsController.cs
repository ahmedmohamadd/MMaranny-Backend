using Maranny.Application.DTOs.Bookings;
using Maranny.Application.DTOs.Sessions;
using Maranny.Application.Features.Bookings.ApproveBooking;
using Maranny.Application.Features.Bookings.BookSession;
using Maranny.Application.Features.Bookings.CancelBooking;
using Maranny.Application.Features.Bookings.CoachCancelSession;
using Maranny.Application.Features.Bookings.DeclineBooking;
using Maranny.Application.Features.Bookings.GetBookingDetails;
using Maranny.Application.Features.Bookings.GetCoachBookings;
using Maranny.Application.Features.Bookings.GetMyBookings;
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
        private readonly IBookSessionUseCase _bookSessionUseCase;
        private readonly IApproveBookingUseCase _approveBookingUseCase;
        private readonly IDeclineBookingUseCase _declineBookingUseCase;
        private readonly ICancelBookingUseCase _cancelBookingUseCase;
        private readonly ICoachCancelSessionUseCase _coachCancelSessionUseCase;
        private readonly IGetMyBookingsUseCase _getMyBookingsUseCase;
        private readonly IGetBookingDetailsUseCase _getBookingDetailsUseCase;
        private readonly IGetCoachBookingsUseCase _getCoachBookingsUseCase;

        public BookingsController(
            IBookSessionUseCase bookSessionUseCase,
            IApproveBookingUseCase approveBookingUseCase,
            IDeclineBookingUseCase declineBookingUseCase,
            ICancelBookingUseCase cancelBookingUseCase,
            ICoachCancelSessionUseCase coachCancelSessionUseCase,
            IGetMyBookingsUseCase getMyBookingsUseCase,
            IGetBookingDetailsUseCase getBookingDetailsUseCase,
            IGetCoachBookingsUseCase getCoachBookingsUseCase)
        {
            _bookSessionUseCase = bookSessionUseCase;
            _approveBookingUseCase = approveBookingUseCase;
            _declineBookingUseCase = declineBookingUseCase;
            _cancelBookingUseCase = cancelBookingUseCase;
            _coachCancelSessionUseCase = coachCancelSessionUseCase;
            _getMyBookingsUseCase = getMyBookingsUseCase;
            _getBookingDetailsUseCase = getBookingDetailsUseCase;
            _getCoachBookingsUseCase = getCoachBookingsUseCase;
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> BookSession(CreateBookingDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _bookSessionUseCase.ExecuteAsync(new BookSessionCommand(userId, dto));
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpGet("my")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetMyBookings(
            [FromQuery] string? status, [FromQuery] string? tab,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _getMyBookingsUseCase.ExecuteAsync(
                new GetMyBookingsQuery(userId, status, tab, page, pageSize));

            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpGet("{bookingId:int}")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetBookingDetails(int bookingId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _getBookingDetailsUseCase.ExecuteAsync(
                new GetBookingDetailsQuery(userId, bookingId));

            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpGet("coach/my")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GetCoachBookings(
            [FromQuery] string? status, [FromQuery] string? tab,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _getCoachBookingsUseCase.ExecuteAsync(
                new GetCoachBookingsQuery(userId, status, tab, page, pageSize));

            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpPut("{bookingId}/approve")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> ApproveBooking(int bookingId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _approveBookingUseCase.ExecuteAsync(new ApproveBookingCommand(userId, bookingId));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(new { message = result.Value });
        }

        [HttpPut("{bookingId}/decline")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> DeclineBooking(int bookingId, [FromBody] CoachBookingActionDto? dto = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _declineBookingUseCase.ExecuteAsync(new DeclineBookingCommand(userId, bookingId, dto));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(new { message = result.Value });
        }

        [HttpPut("{bookingId}/cancel")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> CancelBooking(int bookingId, [FromQuery] string? reason = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _cancelBookingUseCase.ExecuteAsync(new CancelBookingCommand(userId, bookingId, reason));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpPut("session/{sessionId}/cancel-by-coach")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CoachCancelSession(int sessionId, [FromQuery] string? reason = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _coachCancelSessionUseCase.ExecuteAsync(new CoachCancelSessionCommand(userId, sessionId, reason));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(result.Value);
        }
    }
}
