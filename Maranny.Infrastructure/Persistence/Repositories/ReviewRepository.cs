using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Entities;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class ReviewRepository : IReviewRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public ReviewRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<Review?> GetByIdAsync(int reviewId)
        {
            return _dbContext.Reviews.FindAsync(reviewId).AsTask();
        }

        public Task<bool> ClientReviewedSessionAsync(int clientId, int sessionId)
        {
            return _dbContext.Reviews
                .AnyAsync(r => r.ClientID == clientId && r.SessionID == sessionId);
        }

        public async Task AddAsync(Review review)
        {
            await _dbContext.Reviews.AddAsync(review);
        }

        public async Task<decimal> GetCoachAverageRatingAsync(int coachId)
        {
            return await _dbContext.Reviews
                .Where(r => r.CoachID == coachId)
                .AverageAsync(r => (decimal?)r.Rating) ?? 0;
        }
    }
}
