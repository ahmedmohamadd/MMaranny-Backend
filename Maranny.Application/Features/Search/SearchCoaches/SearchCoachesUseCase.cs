using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Search.SearchCoaches
{
    public sealed class SearchCoachesUseCase : ISearchCoachesUseCase
    {
        private readonly ISearchReadRepository _search;

        public SearchCoachesUseCase(ISearchReadRepository search)
        {
            _search = search;
        }

        public async Task<Result<object>> ExecuteAsync(SearchCoachesQuery query)
        {
            return Result<object>.Success(await _search.SearchCoachesAsync(query.Search));
        }
    }
}
