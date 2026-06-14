using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sports.GetSports
{
    public interface IGetSportsUseCase
    {
        Task<Result<IReadOnlyCollection<object>>> ExecuteAsync();
    }
}
