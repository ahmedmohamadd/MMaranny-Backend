using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Bookings.GetCoachBookings
{
    public sealed class GetCoachBookingsUseCase : IGetCoachBookingsUseCase
    {
        private readonly IBookingReadRepository _bookings;

        public GetCoachBookingsUseCase(IBookingReadRepository bookings)
        {
            _bookings = bookings;
        }

        public async Task<Result<object>> ExecuteAsync(GetCoachBookingsQuery query)
        {
            var data = await _bookings.GetCoachBookingsAsync(
                query.UserId,
                query.Status,
                query.Tab,
                query.Page,
                query.PageSize);

            return data == null
                ? Result<object>.Failure(new Error("Coach.NotFound", "Coach profile not found", ErrorType.NotFound))
                : Result<object>.Success(data);
        }
    }
}
