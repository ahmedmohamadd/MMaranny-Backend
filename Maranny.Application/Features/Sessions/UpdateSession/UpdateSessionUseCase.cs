using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Core.Enums;

namespace Maranny.Application.Features.Sessions.UpdateSession
{
    public sealed class UpdateSessionUseCase : IUpdateSessionUseCase
    {
        private readonly ICoachRepository _coaches;
        private readonly ISessionRepository _sessions;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateSessionUseCase(
            ICoachRepository coaches,
            ISessionRepository sessions,
            IUnitOfWork unitOfWork)
        {
            _coaches = coaches;
            _sessions = sessions;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<string>> ExecuteAsync(UpdateSessionCommand command)
        {
            var coach = await _coaches.GetByUserIdAsync(command.UserId);
            if (coach == null)
            {
                return Failure("Coach profile not found");
            }

            var session = await _sessions.GetByIdAsync(command.SessionId);
            if (session == null)
            {
                return Failure("Session not found");
            }

            if (session.CoachID != coach.CoachID)
            {
                return Failure("Forbidden");
            }

            if (command.Session.SessionDate.HasValue)
            {
                session.SessionDate = command.Session.SessionDate.Value;
            }

            if (!string.IsNullOrEmpty(command.Session.SessionType))
            {
                session.SessionType = command.Session.SessionType;
            }

            if (!string.IsNullOrEmpty(command.Session.Location))
            {
                session.Location = command.Session.Location;
            }

            if (command.Session.MaxParticipants.HasValue)
            {
                session.MaxParticipants = command.Session.MaxParticipants.Value;
            }

            if (command.Session.Start_Time.HasValue)
            {
                session.Start_Time = command.Session.Start_Time.Value;
            }

            if (command.Session.End_Time.HasValue)
            {
                session.End_Time = command.Session.End_Time.Value;
            }

            if (!string.IsNullOrEmpty(command.Session.Status) &&
                Enum.TryParse<SessionStatus>(command.Session.Status, out var status))
            {
                session.Status = status;
            }

            await _unitOfWork.SaveChangesAsync();
            return Result<string>.Success("Session updated successfully");
        }

        private static Result<string> Failure(string message)
        {
            return Result<string>.Failure(new Error("Session.UpdateFailed", message, ErrorType.Failure));
        }
    }
}
