using Maranny.Application.DTOs.Sessions;

namespace Maranny.Application.Features.Bookings.BookSession
{
    public sealed record BookSessionCommand(int UserId, CreateBookingDto Booking);
}
