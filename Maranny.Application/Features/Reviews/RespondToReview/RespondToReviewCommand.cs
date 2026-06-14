using Maranny.Application.DTOs.Reviews;

namespace Maranny.Application.Features.Reviews.RespondToReview
{
    public sealed record RespondToReviewCommand(int UserId, int ReviewId, CoachResponseDto Response);
}
