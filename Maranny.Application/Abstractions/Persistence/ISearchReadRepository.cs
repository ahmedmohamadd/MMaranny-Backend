using Maranny.Application.DTOs.Search;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface ISearchReadRepository
    {
        Task<object> SearchCoachesAsync(CoachSearchDto search);
        Task<object?> GetCoachDetailsAsync(int coachId);
    }
}
