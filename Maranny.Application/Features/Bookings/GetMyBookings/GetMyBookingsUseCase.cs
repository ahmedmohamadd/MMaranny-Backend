using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.GetMyBookings
{
    public sealed class GetMyBookingsUseCase : IGetMyBookingsUseCase
    {
        private readonly IBookingReadRepository _bookings;

        public GetMyBookingsUseCase(IBookingReadRepository bookings)
        {
            _bookings = bookings;
        }

        public async Task<Result<object>> ExecuteAsync(GetMyBookingsQuery query)
        {
            var data = await _bookings.GetClientBookingsAsync(
                query.UserId,
                query.Status,
                query.Tab,
                query.Page,
                query.PageSize);

            return data == null
                ? Result<object>.Failure(new Error("Client.NotFound", "Client profile not found", ErrorType.NotFound))
                : Result<object>.Success(data);
        }
    }
}
