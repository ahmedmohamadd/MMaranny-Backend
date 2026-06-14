using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sessions.GetMySessions
{
    public interface IGetMySessionsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetMySessionsQuery query);
    }
}
