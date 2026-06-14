using Maranny.Application.Abstractions.Persistence;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.ReadRepositories
{
    public sealed class ReviewReadRepository : IReviewReadRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public ReviewReadRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<object?> GetCoachReviewsAsync(int coachId, int page, int pageSize)
        {
            var coach = await _dbContext.Coaches.FindAsync(coachId);
            if (coach == null)
            {
                return null;
            }

            var query = _dbContext.Reviews
                .Include(r => r.Client)
                .Where(r => r.CoachID == coachId)
                .OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var reviews = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.ReviewID,
                    r.Rating,
                    r.Comment,
                    r.CoachResponse,
                    r.ResponseDate,
                    r.CreatedAt,
                    client = new
                    {
                        name = r.Client.F_name + " " + r.Client.L_name,
                        profilePicture = r.Client.URL
                    }
                })
                .ToListAsync();

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                averageRating = coach.AvgRating,
                reviews
            };
        }
    }
}
