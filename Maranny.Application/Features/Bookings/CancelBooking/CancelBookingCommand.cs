namespace Maranny.Application.Features.Bookings.CancelBooking
{
    public sealed record CancelBookingCommand(int UserId, int BookingId, string? Reason);
}
