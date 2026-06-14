using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sessions.UpdateSession
{
    public interface IUpdateSessionUseCase
    {
        Task<Result<string>> ExecuteAsync(UpdateSessionCommand command);
    }
}
