using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Reviews.GetCoachReviews
{
    public sealed class GetCoachReviewsUseCase : IGetCoachReviewsUseCase
    {
        private readonly IReviewReadRepository _reviews;

        public GetCoachReviewsUseCase(IReviewReadRepository reviews)
        {
            _reviews = reviews;
        }

        public async Task<Result<object>> ExecuteAsync(GetCoachReviewsQuery query)
        {
            var data = await _reviews.GetCoachReviewsAsync(query.CoachId, query.Page, query.PageSize);

            return data == null
                ? Result<object>.Failure(new Error("Coach.NotFound", "Coach not found", ErrorType.NotFound))
                : Result<object>.Success(data);
        }
    }
}
