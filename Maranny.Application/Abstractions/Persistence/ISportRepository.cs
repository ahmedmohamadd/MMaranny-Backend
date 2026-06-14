using Maranny.Core.Entities;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface ISportRepository
    {
        Task<IReadOnlyCollection<object>> GetAllAsync();
        Task<bool> ExistsAsync(int sportId);
        Task<IReadOnlyCollection<int>> GetExistingIdsAsync(IEnumerable<int> sportIds);
        Task AddAsync(Sport sport);
    }
}
