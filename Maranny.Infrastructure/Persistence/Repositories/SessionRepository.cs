using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class SessionRepository : ISessionRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public SessionRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<TrainingSession?> GetByIdAsync(int sessionId)
        {
            return _dbContext.TrainingSessions.FindAsync(sessionId).AsTask();
        }

        public Task<bool> CoachHasOverlappingSessionAsync(
            int coachId,
            DateTime sessionDate,
            TimeSpan startTime,
            TimeSpan endTime,
            int? excludingSessionId = null)
        {
            var query = _dbContext.TrainingSessions
                .Where(s => s.CoachID == coachId &&
                            s.SessionDate.Date == sessionDate.Date &&
                            s.Status != SessionStatus.Cancelled);

            if (excludingSessionId.HasValue)
            {
                query = query.Where(s => s.SessionID != excludingSessionId.Value);
            }

            return query.AnyAsync(s =>
                (startTime >= s.Start_Time && startTime < s.End_Time) ||
                (endTime > s.Start_Time && endTime <= s.End_Time) ||
                (startTime <= s.Start_Time && endTime >= s.End_Time));
        }

        public async Task AddAsync(TrainingSession session)
        {
            await _dbContext.TrainingSessions.AddAsync(session);
        }
    }
}
