using Maranny.Application.Abstractions.Persistence;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class CoachSportRepository : ICoachSportRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public CoachSportRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<decimal?> GetSessionPriceAsync(int coachId, int sportId)
        {
            return _dbContext.CoachSports
                .Where(cs => cs.CoachID == coachId && cs.SportID == sportId)
                .Select(cs => cs.PricePerSession)
                .FirstOrDefaultAsync();
        }
    }
}
