namespace Maranny.Application.Abstractions.Persistence
{
    public interface ICoachSportRepository
    {
        Task<decimal?> GetSessionPriceAsync(int coachId, int sportId);
    }
}
