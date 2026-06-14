namespace Maranny.Application.Abstractions.Persistence
{
    public interface ISessionReadRepository
    {
        Task<object?> GetCoachSessionsAsync(int userId, string? status, int page, int pageSize);

        Task<object> GetAvailableSessionsAsync(
            int? coachId,
            int? sportId,
            DateTime? date,
            int page,
            int pageSize);
    }
}
