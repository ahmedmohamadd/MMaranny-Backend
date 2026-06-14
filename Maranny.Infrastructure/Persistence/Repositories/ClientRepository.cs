using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Entities;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class ClientRepository : IClientRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public ClientRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<Client?> GetByUserIdAsync(int userId)
        {
            return _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public Task<int> GetUserIdByClientIdAsync(int clientId)
        {
            return _dbContext.Clients
                .Where(c => c.ClientID == clientId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();
        }
    }
}
