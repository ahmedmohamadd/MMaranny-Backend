namespace Maranny.Application.Features.Bookings.GetMyBookings
{
    public sealed record GetMyBookingsQuery(
        int UserId,
        string? Status,
        string? Tab,
        int Page,
        int PageSize);
}
