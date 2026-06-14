using Maranny.Core.Entities;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface IReviewRepository
    {
        Task<Review?> GetByIdAsync(int reviewId);
        Task<bool> ClientReviewedSessionAsync(int clientId, int sessionId);
        Task AddAsync(Review review);
        Task<decimal> GetCoachAverageRatingAsync(int coachId);
    }
}
