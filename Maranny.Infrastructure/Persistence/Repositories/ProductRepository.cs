using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Entities;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class ProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public ProductRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<Product?> GetByIdAsync(int productId)
        {
            return _dbContext.Products.FindAsync(productId).AsTask();
        }

        public async Task AddAsync(Product product)
        {
            await _dbContext.Products.AddAsync(product);
        }

        public async Task AddSportProductAsync(SportProduct sportProduct)
        {
            await _dbContext.SportProducts.AddAsync(sportProduct);
        }

        public async Task RemoveProductSportsAsync(int productId)
        {
            var sportProducts = await _dbContext.SportProducts
                .Where(sp => sp.ProductID == productId)
                .ToListAsync();

            _dbContext.SportProducts.RemoveRange(sportProducts);
        }

        public void Remove(Product product)
        {
            _dbContext.Products.Remove(product);
        }
    }
}
