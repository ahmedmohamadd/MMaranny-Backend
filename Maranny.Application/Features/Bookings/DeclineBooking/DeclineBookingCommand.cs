using Maranny.Application.DTOs.Bookings;

namespace Maranny.Application.Features.Bookings.DeclineBooking
{
    public sealed record DeclineBookingCommand(
        int UserId,
        int BookingId,
        CoachBookingActionDto? Action);
}
