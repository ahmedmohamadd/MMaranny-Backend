using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sports.CreateSport
{
    public interface ICreateSportUseCase
    {
        Task<Result<object>> ExecuteAsync(CreateSportCommand command);
    }
}
