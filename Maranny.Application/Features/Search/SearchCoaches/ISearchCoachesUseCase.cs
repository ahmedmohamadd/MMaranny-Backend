using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Search.SearchCoaches
{
    public interface ISearchCoachesUseCase
    {
        Task<Result<object>> ExecuteAsync(SearchCoachesQuery query);
    }
}
