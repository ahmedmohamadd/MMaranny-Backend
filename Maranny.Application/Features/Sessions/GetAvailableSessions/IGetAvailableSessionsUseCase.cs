using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sessions.GetAvailableSessions
{
    public interface IGetAvailableSessionsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetAvailableSessionsQuery query);
    }
}
