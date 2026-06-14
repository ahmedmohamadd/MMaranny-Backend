using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Core.Policies;

namespace Maranny.Application.Features.Sessions.CreateSession
{
    public sealed class CreateSessionUseCase : ICreateSessionUseCase
    {
        private readonly ICoachRepository _coaches;
        private readonly ISessionRepository _sessions;
        private readonly ISportRepository _sports;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;

        public CreateSessionUseCase(
            ICoachRepository coaches,
            ISessionRepository sessions,
            ISportRepository sports,
            IUnitOfWork unitOfWork,
            IClock clock)
        {
            _coaches = coaches;
            _sessions = sessions;
            _sports = sports;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<Result<object>> ExecuteAsync(CreateSessionCommand command)
        {
            var coach = await _coaches.GetByUserIdAsync(command.UserId);
            if (coach == null)
            {
                return Failure("Coach profile not found");
            }

            if (!SessionPolicy.IsVerifiedCoach(coach.VerificationStatus))
            {
                return Failure("Coach must be verified before creating sessions");
            }

            if (command.Session.SessionDate.Date < _clock.UtcNow.Date)
            {
                return Failure("Cannot create session in the past");
            }

            if (command.Session.End_Time <= command.Session.Start_Time)
            {
                return Failure("End time must be after start time");
            }

            if (!await _sports.ExistsAsync(command.Session.SportID))
            {
                return Failure("Sport not found");
            }

            var hasOverlap = await _sessions.CoachHasOverlappingSessionAsync(
                coach.CoachID,
                command.Session.SessionDate,
                command.Session.Start_Time,
                command.Session.End_Time);

            if (hasOverlap)
            {
                return Failure("You have an overlapping session at this time");
            }

            var session = new TrainingSession
            {
                CoachID = coach.CoachID,
                SportID = command.Session.SportID,
                SessionDate = command.Session.SessionDate,
                SessionType = command.Session.SessionType,
                Location = command.Session.Location,
                MaxParticipants = command.Session.MaxParticipants,
                Start_Time = command.Session.Start_Time,
                End_Time = command.Session.End_Time,
                Status = SessionStatus.Scheduled
            };

            await _sessions.AddAsync(session);
            await _unitOfWork.SaveChangesAsync();

            return Result<object>.Success(new
            {
                message = "Session created successfully",
                data = new { sessionId = session.SessionID }
            });
        }

        private static Result<object> Failure(string message)
        {
            return Result<object>.Failure(new Error("Session.CreateFailed", message, ErrorType.Failure));
        }
    }
}
