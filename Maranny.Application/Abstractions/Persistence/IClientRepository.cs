using Maranny.Core.Entities;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface IClientRepository
    {
        Task<Client?> GetByUserIdAsync(int userId);
        Task<int> GetUserIdByClientIdAsync(int clientId);
    }
}
