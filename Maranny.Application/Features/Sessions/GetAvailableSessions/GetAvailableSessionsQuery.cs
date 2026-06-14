namespace Maranny.Application.Features.Sessions.GetAvailableSessions
{
    public sealed record GetAvailableSessionsQuery(
        int? CoachId,
        int? SportId,
        DateTime? Date,
        int Page,
        int PageSize);
}
