using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Core.Enums;

namespace Maranny.Application.Features.Sessions.CancelSession
{
    public sealed class CancelSessionUseCase : ICancelSessionUseCase
    {
        private readonly ICoachRepository _coaches;
        private readonly ISessionRepository _sessions;
        private readonly IUnitOfWork _unitOfWork;

        public CancelSessionUseCase(
            ICoachRepository coaches,
            ISessionRepository sessions,
            IUnitOfWork unitOfWork)
        {
            _coaches = coaches;
            _sessions = sessions;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<string>> ExecuteAsync(CancelSessionCommand command)
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

            session.Status = SessionStatus.Cancelled;
            await _unitOfWork.SaveChangesAsync();
            return Result<string>.Success("Session cancelled successfully");
        }

        private static Result<string> Failure(string message)
        {
            return Result<string>.Failure(new Error("Session.CancelFailed", message, ErrorType.Failure));
        }
    }
}
