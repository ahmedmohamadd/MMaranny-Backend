using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Reviews.RespondToReview
{
    public interface IRespondToReviewUseCase
    {
        Task<Result<string>> ExecuteAsync(RespondToReviewCommand command);
    }
}
