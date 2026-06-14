using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sessions.CancelSession
{
    public interface ICancelSessionUseCase
    {
        Task<Result<string>> ExecuteAsync(CancelSessionCommand command);
    }
}
