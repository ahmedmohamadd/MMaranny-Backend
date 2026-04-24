using Maranny.Application.DTOs.Reviews;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> SubmitReview(SubmitReviewDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _reviewService.SubmitReviewAsync(userId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message, data });
        }

        [HttpGet("coach/{coachId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCoachReviews(int coachId,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var (success, data) = await _reviewService.GetCoachReviewsAsync(coachId, page, pageSize);
            if (!success) return NotFound(new { error = "Coach not found" });
            return Ok(data);
        }

        [HttpPut("{reviewId}/response")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> RespondToReview(int reviewId, CoachResponseDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _reviewService.RespondToReviewAsync(userId, reviewId, dto);
            if (message == "Forbidden") return Forbid();
            if (!success) return NotFound(new { error = message });
            return Ok(new { message });
        }
    }
}