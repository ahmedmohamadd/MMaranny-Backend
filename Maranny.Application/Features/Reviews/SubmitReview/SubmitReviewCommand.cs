using Maranny.Application.DTOs.Reviews;

namespace Maranny.Application.Features.Reviews.SubmitReview
{
    public sealed record SubmitReviewCommand(int UserId, SubmitReviewDto Review);
}
