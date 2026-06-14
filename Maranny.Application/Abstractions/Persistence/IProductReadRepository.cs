namespace Maranny.Application.Abstractions.Persistence
{
    public interface IProductReadRepository
    {
        Task<object> GetAllAsync(
            int? categoryId,
            int? sportId,
            decimal? maxPrice,
            string? condition,
            string? search,
            int page,
            int pageSize);

        Task<object?> GetByIdAsync(int productId);
    }
}
