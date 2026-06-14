using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sessions.CreateSession
{
    public interface ICreateSessionUseCase
    {
        Task<Result<object>> ExecuteAsync(CreateSessionCommand command);
    }
}
