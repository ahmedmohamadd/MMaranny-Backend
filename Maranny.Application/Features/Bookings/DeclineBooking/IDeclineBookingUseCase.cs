using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.DeclineBooking
{
    public interface IDeclineBookingUseCase
    {
        Task<Result<string>> ExecuteAsync(DeclineBookingCommand command);
    }
}
