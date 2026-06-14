using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.ApproveBooking
{
    public interface IApproveBookingUseCase
    {
        Task<Result<string>> ExecuteAsync(ApproveBookingCommand command);
    }
}
