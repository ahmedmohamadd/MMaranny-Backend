using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Reviews.SubmitReview
{
    public interface ISubmitReviewUseCase
    {
        Task<Result<object>> ExecuteAsync(SubmitReviewCommand command);
    }
}
