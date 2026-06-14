using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Entities;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class SportRepository : ISportRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public SportRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyCollection<object>> GetAllAsync()
        {
            return await _dbContext.Sports
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync<object>();
        }

        public Task<bool> ExistsAsync(int sportId)
        {
            return _dbContext.Sports.AnyAsync(s => s.Id == sportId);
        }

        public async Task<IReadOnlyCollection<int>> GetExistingIdsAsync(IEnumerable<int> sportIds)
        {
            var ids = sportIds.Distinct().ToList();

            return await _dbContext.Sports
                .Where(s => ids.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync();
        }

        public async Task AddAsync(Sport sport)
        {
            await _dbContext.Sports.AddAsync(sport);
        }
    }
}
