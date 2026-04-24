using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Sessions;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Services
{
    public class SessionsService : ISessionService
    {
        private readonly ApplicationDbContext _dbContext;

        public SessionsService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(bool success, string message, object? data)> CreateSessionAsync(int userId, CreateSessionDto dto)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null) return (false, "Coach profile not found", null);

            if (coach.VerificationStatus != VerificationStatus.Verified &&
                coach.VerificationStatus != VerificationStatus.Approved)
                return (false, "Coach must be verified before creating sessions", null);

            if (dto.SessionDate.Date < DateTime.UtcNow.Date)
                return (false, "Cannot create session in the past", null);

            if (dto.End_Time <= dto.Start_Time)
                return (false, "End time must be after start time", null);

            var sport = await _dbContext.Sports.FindAsync(dto.SportID);
            if (sport == null) return (false, "Sport not found", null);

            var overlapping = await _dbContext.TrainingSessions
                .Where(s => s.CoachID == coach.CoachID &&
                            s.SessionDate.Date == dto.SessionDate.Date &&
                            s.Status != SessionStatus.Cancelled &&
                            ((dto.Start_Time >= s.Start_Time && dto.Start_Time < s.End_Time) ||
                             (dto.End_Time > s.Start_Time && dto.End_Time <= s.End_Time) ||
                             (dto.Start_Time <= s.Start_Time && dto.End_Time >= s.End_Time)))
                .FirstOrDefaultAsync();

            if (overlapping != null)
                return (false, "You have an overlapping session at this time", null);

            var session = new TrainingSession
            {
                CoachID = coach.CoachID,
                SportID = dto.SportID,
                SessionDate = dto.SessionDate,
                SessionType = dto.SessionType,
                Location = dto.Location,
                MaxParticipants = dto.MaxParticipants,
                Start_Time = dto.Start_Time,
                End_Time = dto.End_Time,
                Status = SessionStatus.Scheduled
            };

            _dbContext.TrainingSessions.Add(session);
            await _dbContext.SaveChangesAsync();

            return (true, "Session created successfully", new { sessionId = session.SessionID });
        }

        public async Task<(bool success, object? data)> GetMySessionsAsync(int userId, string? status, int page, int pageSize)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null) return (false, null);

            var query = _dbContext.TrainingSessions
                .Include(s => s.Sport)
                .Where(s => s.CoachID == coach.CoachID);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<SessionStatus>(status, out var sessionStatus))
                query = query.Where(s => s.Status == sessionStatus);

            var totalCount = await query.CountAsync();

            var sessions = await query
                .OrderByDescending(s => s.SessionDate)
                .Skip((page - 1) * pageSize).Take(pageSize)
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
                        .Select(cs => cs.PricePerSession).FirstOrDefault(),
                    BookedCount = _dbContext.ClientSessions.Count(cs => cs.SessionID == s.SessionID),
                    AvailableSlots = s.MaxParticipants - _dbContext.ClientSessions.Count(cs => cs.SessionID == s.SessionID)
                }).ToListAsync();

            return (true, new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                sessions
            });
        }

        public async Task<object> GetAvailableSessionsAsync(int? coachId, int? sportId, DateTime? date, int page, int pageSize)
        {
            var query = _dbContext.TrainingSessions
                .Include(s => s.Sport)
                .Include(s => s.Coach)
                .Where(s => s.Status == SessionStatus.Scheduled &&
                            s.SessionDate >= DateTime.UtcNow.Date);

            if (coachId.HasValue) query = query.Where(s => s.CoachID == coachId.Value);
            if (sportId.HasValue) query = query.Where(s => s.SportID == sportId.Value);
            if (date.HasValue) query = query.Where(s => s.SessionDate.Date == date.Value.Date);

            query = query.Where(s =>
                !s.MaxParticipants.HasValue ||
                _dbContext.ClientSessions.Count(cs => cs.SessionID == s.SessionID) < s.MaxParticipants.Value);

            var totalCount = await query.CountAsync();

            var sessions = await query
                .OrderBy(s => s.SessionDate).ThenBy(s => s.Start_Time)
                .Skip((page - 1) * pageSize).Take(pageSize)
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
                        .Select(cs => cs.PricePerSession).FirstOrDefault(),
                    Coach = new
                    {
                        s.Coach.CoachID,
                        Name = s.Coach.F_name + " " + s.Coach.L_name,
                        s.Coach.AvgRating,
                        s.Coach.ExperienceYears,
                        VerificationStatus = s.Coach.VerificationStatus.ToString()
                    },
                    AvailableSlots = s.MaxParticipants - _dbContext.ClientSessions.Count(cs => cs.SessionID == s.SessionID)
                }).ToListAsync();

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                sessions
            };
        }

        public async Task<(bool success, string message)> UpdateSessionAsync(int userId, int sessionId, UpdateSessionDto dto)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null) return (false, "Coach profile not found");

            var session = await _dbContext.TrainingSessions.FindAsync(sessionId);
            if (session == null) return (false, "Session not found");
            if (session.CoachID != coach.CoachID) return (false, "Forbidden");

            if (dto.SessionDate.HasValue) session.SessionDate = dto.SessionDate.Value;
            if (!string.IsNullOrEmpty(dto.SessionType)) session.SessionType = dto.SessionType;
            if (!string.IsNullOrEmpty(dto.Location)) session.Location = dto.Location;
            if (dto.MaxParticipants.HasValue) session.MaxParticipants = dto.MaxParticipants.Value;
            if (dto.Start_Time.HasValue) session.Start_Time = dto.Start_Time.Value;
            if (dto.End_Time.HasValue) session.End_Time = dto.End_Time.Value;
            if (!string.IsNullOrEmpty(dto.Status) && Enum.TryParse<SessionStatus>(dto.Status, out var status))
                session.Status = status;

            await _dbContext.SaveChangesAsync();
            return (true, "Session updated successfully");
        }

        public async Task<(bool success, string message)> CancelSessionAsync(int userId, int sessionId)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null) return (false, "Coach profile not found");

            var session = await _dbContext.TrainingSessions.FindAsync(sessionId);
            if (session == null) return (false, "Session not found");
            if (session.CoachID != coach.CoachID) return (false, "Forbidden");

            session.Status = SessionStatus.Cancelled;
            await _dbContext.SaveChangesAsync();
            return (true, "Session cancelled successfully");
        }
    }
}