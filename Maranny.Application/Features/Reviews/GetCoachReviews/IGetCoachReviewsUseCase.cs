using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Reviews.GetCoachReviews
{
    public interface IGetCoachReviewsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetCoachReviewsQuery query);
    }
}
