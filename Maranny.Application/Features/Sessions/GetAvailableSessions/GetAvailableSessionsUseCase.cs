using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sessions.GetAvailableSessions
{
    public sealed class GetAvailableSessionsUseCase : IGetAvailableSessionsUseCase
    {
        private readonly ISessionReadRepository _sessions;

        public GetAvailableSessionsUseCase(ISessionReadRepository sessions)
        {
            _sessions = sessions;
        }

        public async Task<Result<object>> ExecuteAsync(GetAvailableSessionsQuery query)
        {
            var data = await _sessions.GetAvailableSessionsAsync(
                query.CoachId,
                query.SportId,
                query.Date,
                query.Page,
                query.PageSize);

            return Result<object>.Success(data);
        }
    }
}
