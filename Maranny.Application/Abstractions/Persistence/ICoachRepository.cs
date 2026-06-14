using Maranny.Core.Entities;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface ICoachRepository
    {
        Task<Coach?> GetByIdAsync(int coachId);
        Task<Coach?> GetByUserIdAsync(int userId);
        Task<int> GetUserIdByCoachIdAsync(int coachId);
    }
}
