using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.GetBookingDetails
{
    public sealed class GetBookingDetailsUseCase : IGetBookingDetailsUseCase
    {
        private readonly IBookingReadRepository _bookings;

        public GetBookingDetailsUseCase(IBookingReadRepository bookings)
        {
            _bookings = bookings;
        }

        public async Task<Result<object>> ExecuteAsync(GetBookingDetailsQuery query)
        {
            var (error, data) = await _bookings.GetClientBookingDetailsAsync(query.UserId, query.BookingId);

            return error == null
                ? Result<object>.Success(data!)
                : Result<object>.Failure(new Error("Booking.DetailsFailed", error, ErrorType.NotFound));
        }
    }
}
