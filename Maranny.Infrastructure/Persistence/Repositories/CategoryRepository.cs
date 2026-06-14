using Maranny.Application.Abstractions.Persistence;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class CategoryRepository : ICategoryRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public CategoryRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<bool> ExistsAsync(int categoryId)
        {
            return _dbContext.Categories.AnyAsync(c => c.CategoryID == categoryId);
        }
    }
}
