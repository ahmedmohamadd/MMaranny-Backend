namespace Maranny.Application.Features.Sessions.GetMySessions
{
    public sealed record GetMySessionsQuery(int UserId, string? Status, int Page, int PageSize);
}
