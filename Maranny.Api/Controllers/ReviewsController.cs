using Maranny.Application.DTOs.Reviews;
using Maranny.Application.Features.Reviews.GetCoachReviews;
using Maranny.Application.Features.Reviews.RespondToReview;
using Maranny.Application.Features.Reviews.SubmitReview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly ISubmitReviewUseCase _submitReviewUseCase;
        private readonly IGetCoachReviewsUseCase _getCoachReviewsUseCase;
        private readonly IRespondToReviewUseCase _respondToReviewUseCase;

        public ReviewsController(
            ISubmitReviewUseCase submitReviewUseCase,
            IGetCoachReviewsUseCase getCoachReviewsUseCase,
            IRespondToReviewUseCase respondToReviewUseCase)
        {
            _submitReviewUseCase = submitReviewUseCase;
            _getCoachReviewsUseCase = getCoachReviewsUseCase;
            _respondToReviewUseCase = respondToReviewUseCase;
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> SubmitReview(SubmitReviewDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _submitReviewUseCase.ExecuteAsync(new SubmitReviewCommand(userId, dto));
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpGet("coach/{coachId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCoachReviews(int coachId,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _getCoachReviewsUseCase.ExecuteAsync(new GetCoachReviewsQuery(coachId, page, pageSize));
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpPut("{reviewId}/response")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> RespondToReview(int reviewId, CoachResponseDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _respondToReviewUseCase.ExecuteAsync(new RespondToReviewCommand(userId, reviewId, dto));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(new { message = result.Value });
        }
    }
}
