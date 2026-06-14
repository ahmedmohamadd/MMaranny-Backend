using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.ReadRepositories
{
    public sealed class SessionReadRepository : ISessionReadRepository
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IClock _clock;

        public SessionReadRepository(ApplicationDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext;
            _clock = clock;
        }

        public async Task<object?> GetCoachSessionsAsync(int userId, string? status, int page, int pageSize)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return null;
            }

            var query = _dbContext.TrainingSessions
                .Include(s => s.Sport)
                .Where(s => s.CoachID == coach.CoachID);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<SessionStatus>(status, out var sessionStatus))
            {
                query = query.Where(s => s.Status == sessionStatus);
            }

            var totalCount = await query.CountAsync();

            var sessions = await query
                .OrderByDescending(s => s.SessionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.SessionID,
                    s.SessionDate,
                    s.SessionType,
                    s.Location,
                    s.MaxParticipants,
                    s.Start_Time,
                    s.End_Time,
                    Status = s.Status.ToString(),
                    SportName = s.Sport.Name,
                    SportID = s.SportID,
                    Price = _dbContext.CoachSports
                        .Where(cs => cs.CoachID == s.CoachID && cs.SportID == s.SportID)
                        .Select(cs => cs.PricePerSession)
                        .FirstOrDefault(),
                    BookedCount = _dbContext.ClientSessions.Count(cs => cs.SessionID == s.SessionID),
                    AvailableSlots = s.MaxParticipants - _dbContext.ClientSessions.Count(cs => cs.SessionID == s.SessionID)
                })
                .ToListAsync();

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                sessions
            };
        }

        public async Task<object> GetAvailableSessionsAsync(
            int? coachId,
            int? sportId,
            DateTime? date,
            int page,
            int pageSize)
        {
            var query = _dbContext.TrainingSessions
                .Include(s => s.Sport)
                .Include(s => s.Coach)
                .Where(s => s.Status == SessionStatus.Scheduled &&
                            s.SessionDate >= _clock.UtcNow.Date);

            if (coachId.HasValue)
            {
                query = query.Where(s => s.CoachID == coachId.Value);
            }

            if (sportId.HasValue)
            {
                query = query.Where(s => s.SportID == sportId.Value);
            }

            if (date.HasValue)
            {
                query = query.Where(s => s.SessionDate.Date == date.Value.Date);
            }

            query = query.Where(s =>
                !s.MaxParticipants.HasValue ||
                _dbContext.ClientSessions.Count(cs => cs.SessionID == s.SessionID) < s.MaxParticipants.Value);

            var totalCount = await query.CountAsync();

            var sessions = await query
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.Start_Time)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.SessionID,
                    s.SessionDate,
                    s.SessionType,
                    s.Location,
                    s.MaxParticipants,
                    s.Start_Time,
                    s.End_Time,
                    SportName = s.Sport.Name,
                    SportID = s.SportID,
                    Price = _dbContext.CoachSports
                        .Where(cs => cs.CoachID == s.CoachID && cs.SportID == s.SportID)
                        .Select(cs => cs.PricePerSession)
                        .FirstOrDefault(),
                    Coach = new
                    {
                        s.Coach.CoachID,
                        Name = s.Coach.F_name + " " + s.Coach.L_name,
                        s.Coach.AvgRating,
                        s.Coach.ExperienceYears,
                        VerificationStatus = s.Coach.VerificationStatus.ToString()
                    },
                    AvailableSlots = s.MaxParticipants - _dbContext.ClientSessions.Count(cs => cs.SessionID == s.SessionID)
                })
                .ToListAsync();

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                sessions
            };
        }
    }
}
