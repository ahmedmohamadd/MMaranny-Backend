namespace Maranny.Application.Abstractions.Persistence
{
    public interface ICategoryRepository
    {
        Task<bool> ExistsAsync(int categoryId);
    }
}
