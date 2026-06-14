namespace Maranny.Application.Features.Bookings.ApproveBooking
{
    public sealed record ApproveBookingCommand(int UserId, int BookingId);
}
