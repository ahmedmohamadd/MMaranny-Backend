using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.CoachCancelSession
{
    public interface ICoachCancelSessionUseCase
    {
        Task<Result<object>> ExecuteAsync(CoachCancelSessionCommand command);
    }
}
