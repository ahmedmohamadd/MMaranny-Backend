using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Sessions.GetMySessions
{
    public sealed class GetMySessionsUseCase : IGetMySessionsUseCase
    {
        private readonly ISessionReadRepository _sessions;

        public GetMySessionsUseCase(ISessionReadRepository sessions)
        {
            _sessions = sessions;
        }

        public async Task<Result<object>> ExecuteAsync(GetMySessionsQuery query)
        {
            var data = await _sessions.GetCoachSessionsAsync(query.UserId, query.Status, query.Page, query.PageSize);

            return data == null
                ? Result<object>.Failure(new Error("Coach.NotFound", "Coach profile not found", ErrorType.NotFound))
                : Result<object>.Success(data);
        }
    }
}
