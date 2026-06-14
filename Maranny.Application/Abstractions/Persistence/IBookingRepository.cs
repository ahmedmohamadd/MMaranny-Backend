using Maranny.Core.Entities;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface IBookingRepository
    {
        Task<Booking?> GetByIdWithSessionAsync(int bookingId);
        Task<Booking?> GetByIdWithSessionAndCoachAsync(int bookingId);
        Task<List<Booking>> GetActiveBySessionIdAsync(int sessionId);
        Task<int> CountSessionBookingsAsync(int sessionId);
        Task<bool> ClientHasSessionAsync(int clientId, int sessionId);
        Task<bool> ClientHasOverlappingSessionAsync(
            int clientId,
            DateTime sessionDate,
            TimeSpan startTime,
            TimeSpan endTime);

        Task AddBookingAsync(Booking booking);
        Task AddClientSessionAsync(ClientSession clientSession);
        Task AddUserInteractionAsync(UserInteraction userInteraction);
    }
}
