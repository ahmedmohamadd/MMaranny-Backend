namespace Maranny.Application.Abstractions.Persistence
{
    public interface IReviewReadRepository
    {
        Task<object?> GetCoachReviewsAsync(int coachId, int page, int pageSize);
    }
}
