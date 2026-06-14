using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.GetCoachBookings
{
    public interface IGetCoachBookingsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetCoachBookingsQuery query);
    }
}
