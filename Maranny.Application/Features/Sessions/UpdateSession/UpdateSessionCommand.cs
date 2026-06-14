using Maranny.Application.DTOs.Sessions;

namespace Maranny.Application.Features.Sessions.UpdateSession
{
    public sealed record UpdateSessionCommand(int UserId, int SessionId, UpdateSessionDto Session);
}
