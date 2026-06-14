namespace Maranny.Application.Features.Bookings.CoachCancelSession
{
    public sealed record CoachCancelSessionCommand(int UserId, int SessionId, string? Reason);
}
