using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Reviews.RespondToReview
{
    public sealed class RespondToReviewUseCase : IRespondToReviewUseCase
    {
        private readonly ICoachRepository _coaches;
        private readonly IReviewRepository _reviews;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;

        public RespondToReviewUseCase(
            ICoachRepository coaches,
            IReviewRepository reviews,
            IUnitOfWork unitOfWork,
            IClock clock)
        {
            _coaches = coaches;
            _reviews = reviews;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<Result<string>> ExecuteAsync(RespondToReviewCommand command)
        {
            var coach = await _coaches.GetByUserIdAsync(command.UserId);
            if (coach == null)
            {
                return Failure("Coach profile not found");
            }

            var review = await _reviews.GetByIdAsync(command.ReviewId);
            if (review == null)
            {
                return Failure("Review not found");
            }

            if (review.CoachID != coach.CoachID)
            {
                return Failure("Forbidden");
            }

            if (!string.IsNullOrEmpty(review.CoachResponse))
            {
                return Failure("You have already responded to this review");
            }

            review.CoachResponse = command.Response.Response;
            review.ResponseDate = _clock.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success("Response added successfully");
        }

        private static Result<string> Failure(string message)
        {
            return Result<string>.Failure(new Error("Review.ResponseFailed", message, ErrorType.Failure));
        }
    }
}
