using Maranny.Application.DTOs.Sessions;

namespace Maranny.Application.Features.Sessions.CreateSession
{
    public sealed record CreateSessionCommand(int UserId, CreateSessionDto Session);
}
