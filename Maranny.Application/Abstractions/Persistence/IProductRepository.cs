using Maranny.Core.Entities;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int productId);
        Task AddAsync(Product product);
        Task AddSportProductAsync(SportProduct sportProduct);
        Task RemoveProductSportsAsync(int productId);
        void Remove(Product product);
    }
}
