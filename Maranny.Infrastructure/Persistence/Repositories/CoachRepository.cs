using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Entities;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class CoachRepository : ICoachRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public CoachRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<Coach?> GetByIdAsync(int coachId)
        {
            return _dbContext.Coaches.FindAsync(coachId).AsTask();
        }

        public Task<Coach?> GetByUserIdAsync(int userId)
        {
            return _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public Task<int> GetUserIdByCoachIdAsync(int coachId)
        {
            return _dbContext.Coaches
                .Where(c => c.CoachID == coachId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();
        }
    }
}
