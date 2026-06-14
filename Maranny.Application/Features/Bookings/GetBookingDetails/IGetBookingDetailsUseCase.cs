using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.GetBookingDetails
{
    public interface IGetBookingDetailsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetBookingDetailsQuery query);
    }
}
