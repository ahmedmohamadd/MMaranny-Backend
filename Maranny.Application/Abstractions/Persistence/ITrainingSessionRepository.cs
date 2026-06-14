using Maranny.Core.Entities;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface ITrainingSessionRepository
    {
        Task<TrainingSession?> GetByIdForBookingAsync(int sessionId);
        Task<TrainingSession?> GetByIdAsync(int sessionId);
    }
}
