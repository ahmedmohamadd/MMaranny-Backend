using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Entities;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class TrainingSessionRepository : ITrainingSessionRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public TrainingSessionRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<TrainingSession?> GetByIdForBookingAsync(int sessionId)
        {
            return _dbContext.TrainingSessions
                .Include(s => s.Coach)
                .FirstOrDefaultAsync(s => s.SessionID == sessionId);
        }

        public Task<TrainingSession?> GetByIdAsync(int sessionId)
        {
            return _dbContext.TrainingSessions.FindAsync(sessionId).AsTask();
        }
    }
}
