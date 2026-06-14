using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class BookingRepository : IBookingRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public BookingRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<Booking?> GetByIdWithSessionAsync(int bookingId)
        {
            return _dbContext.Bookings
                .Include(b => b.TrainingSession)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);
        }

        public Task<Booking?> GetByIdWithSessionAndCoachAsync(int bookingId)
        {
            return _dbContext.Bookings
                .Include(b => b.TrainingSession).ThenInclude(s => s.Coach)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);
        }

        public Task<List<Booking>> GetActiveBySessionIdAsync(int sessionId)
        {
            return _dbContext.Bookings
                .Where(b => b.SessionID == sessionId &&
                            (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                .ToListAsync();
        }

        public Task<int> CountSessionBookingsAsync(int sessionId)
        {
            return _dbContext.ClientSessions.CountAsync(cs => cs.SessionID == sessionId);
        }

        public Task<bool> ClientHasSessionAsync(int clientId, int sessionId)
        {
            return _dbContext.ClientSessions
                .AnyAsync(cs => cs.ClientID == clientId && cs.SessionID == sessionId);
        }

        public Task<bool> ClientHasOverlappingSessionAsync(
            int clientId,
            DateTime sessionDate,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            return _dbContext.ClientSessions
                .Include(cs => cs.TrainingSession)
                .Where(cs => cs.ClientID == clientId &&
                             cs.TrainingSession.SessionDate.Date == sessionDate.Date &&
                             cs.TrainingSession.Status != SessionStatus.Cancelled)
                .AnyAsync(cs =>
                    (startTime >= cs.TrainingSession.Start_Time && startTime < cs.TrainingSession.End_Time) ||
                    (endTime > cs.TrainingSession.Start_Time && endTime <= cs.TrainingSession.End_Time) ||
                    (startTime <= cs.TrainingSession.Start_Time && endTime >= cs.TrainingSession.End_Time));
        }

        public async Task AddBookingAsync(Booking booking)
        {
            await _dbContext.Bookings.AddAsync(booking);
        }

        public async Task AddClientSessionAsync(ClientSession clientSession)
        {
            await _dbContext.ClientSessions.AddAsync(clientSession);
        }

        public async Task AddUserInteractionAsync(UserInteraction userInteraction)
        {
            await _dbContext.UserInteractions.AddAsync(userInteraction);
        }
    }
}
