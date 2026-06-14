namespace Maranny.Application.Features.Bookings.GetCoachBookings
{
    public sealed record GetCoachBookingsQuery(
        int UserId,
        string? Status,
        string? Tab,
        int Page,
        int PageSize);
}
