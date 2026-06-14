using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sports.GetSports
{
    public sealed class GetSportsUseCase : IGetSportsUseCase
    {
        private readonly ISportRepository _sports;

        public GetSportsUseCase(ISportRepository sports)
        {
            _sports = sports;
        }

        public async Task<Result<IReadOnlyCollection<object>>> ExecuteAsync()
        {
            return Result<IReadOnlyCollection<object>>.Success(await _sports.GetAllAsync());
        }
    }
}
