namespace Maranny.Application.Features.Sessions.CancelSession
{
    public sealed record CancelSessionCommand(int UserId, int SessionId);
}
