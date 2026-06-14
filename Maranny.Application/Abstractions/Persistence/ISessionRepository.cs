using Maranny.Core.Entities;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface ISessionRepository
    {
        Task<TrainingSession?> GetByIdAsync(int sessionId);
        Task<bool> CoachHasOverlappingSessionAsync(
            int coachId,
            DateTime sessionDate,
            TimeSpan startTime,
            TimeSpan endTime,
            int? excludingSessionId = null);

        Task AddAsync(TrainingSession session);
    }
}
