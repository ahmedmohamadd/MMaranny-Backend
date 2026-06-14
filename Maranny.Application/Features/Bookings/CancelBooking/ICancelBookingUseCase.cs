using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.CancelBooking
{
    public interface ICancelBookingUseCase
    {
        Task<Result<object>> ExecuteAsync(CancelBookingCommand command);
    }
}
