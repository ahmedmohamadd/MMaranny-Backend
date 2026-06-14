using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.GetMyBookings
{
    public interface IGetMyBookingsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetMyBookingsQuery query);
    }
}
