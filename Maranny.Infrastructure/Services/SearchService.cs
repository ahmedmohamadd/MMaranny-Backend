using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Search;
using Maranny.Application.Interfaces;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Services
{
    public class SearchService : ISearchService
    {
        private readonly ApplicationDbContext _dbContext;

        public SearchService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<object> SearchCoachesAsync(CoachSearchDto dto)
        {
            var query = _dbContext.Coaches
                .Include(c => c.User)
                .Include(c => c.CoachLocations)
                .Include(c => c.CoachSports).ThenInclude(cs => cs.Sport)
                .Where(c => !c.User.IsBlocked);

            if (dto.VerifiedOnly ?? true)
                query = query.Where(c => c.VerificationStatus == VerificationStatus.Approved);

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var nameLower = dto.Name.ToLower();
                query = query.Where(c =>
                    (c.F_name + " " + c.L_name).ToLower().Contains(nameLower) ||
                    c.F_name.ToLower().Contains(nameLower) ||
                    c.L_name.ToLower().Contains(nameLower));
            }

            if (dto.SportID.HasValue)
                query = query.Where(c => c.CoachSports.Any(cs => cs.SportID == dto.SportID.Value));

            if (!string.IsNullOrWhiteSpace(dto.City))
            {
                var cityLower = dto.City.ToLower();
                query = query.Where(c => c.CoachLocations.Any(cl =>
                    cl.WorkingLocation.ToLower().Contains(cityLower)));
            }

            if (dto.MinRating.HasValue)
                query = query.Where(c => c.AvgRating >= dto.MinRating.Value);

            if (dto.MinExperience.HasValue)
                query = query.Where(c => c.ExperienceYears >= dto.MinExperience.Value);

            if (!string.IsNullOrWhiteSpace(dto.Gender) && Enum.TryParse<Gender>(dto.Gender, out var gender))
                query = query.Where(c => c.Gender == gender);

            query = dto.SortBy?.ToLower() switch
            {
                "rating" => dto.SortOrder?.ToLower() == "asc"
                    ? query.OrderBy(c => c.AvgRating)
                    : query.OrderByDescending(c => c.AvgRating),
                "experience" => dto.SortOrder?.ToLower() == "asc"
                    ? query.OrderBy(c => c.ExperienceYears)
                    : query.OrderByDescending(c => c.ExperienceYears),
                "name" => dto.SortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(c => c.F_name)
                    : query.OrderBy(c => c.F_name),
                _ => query.OrderByDescending(c => c.AvgRating)
            };

            var totalCount = await query.CountAsync();

            var coaches = await query
                .Skip((dto.Page - 1) * dto.PageSize)
                .Take(dto.PageSize)
                .Select(c => new
                {
                    c.CoachID,
                    Name = c.F_name + " " + c.L_name,
                    c.Bio,
                    c.ExperienceYears,
                    c.AvgRating,
                    gender = c.Gender.HasValue ? c.Gender.ToString() : null,
                    c.URL,
                    verificationStatus = c.VerificationStatus.ToString(),
                    Email = c.User.Email,
                    PhoneNumber = c.User.PhoneNumber,
                    AvailableDays = string.IsNullOrWhiteSpace(c.AvailabilityStatus)
                        ? new List<string>()
                        : c.AvailabilityStatus.Split(',',
                            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    StartingPrice = c.CoachSports
                        .Where(cs => cs.PricePerSession.HasValue)
                        .OrderBy(cs => cs.PricePerSession)
                        .Select(cs => cs.PricePerSession)
                        .FirstOrDefault(),
                    Sports = c.CoachSports.Select(cs => new
                    {
                        cs.Sport.Id,
                        cs.Sport.Name,
                        cs.Description,
                        cs.PricePerSession,
                        cs.ExperienceYears
                    }).ToList(),
                    Locations = c.CoachLocations.Select(cl => cl.WorkingLocation).ToList(),
                    TotalReviews = _dbContext.Reviews.Count(r => r.CoachID == c.CoachID)
                }).ToListAsync();

            return new
            {
                totalCount,
                page = dto.Page,
                pageSize = dto.PageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)dto.PageSize),
                coaches
            };
        }

        public async Task<(bool success, object? data)> GetCoachDetailsAsync(int coachId, int? userId)
        {
            if (userId.HasValue)
            {
                _dbContext.UserInteractions.Add(new Core.Entities.UserInteraction
                {
                    UserId = userId.Value,
                    CoachId = coachId,
                    Type = "View",
                    Timestamp = DateTime.UtcNow,
                    Context = "Viewed coach profile"
                });
                await _dbContext.SaveChangesAsync();
            }

            var coach = await _dbContext.Coaches
                .Include(c => c.User)
                .Include(c => c.CoachLocations)
                .Include(c => c.CoachSports).ThenInclude(cs => cs.Sport)
                .FirstOrDefaultAsync(c => c.CoachID == coachId);

            if (coach == null) return (false, null);

            var upcomingSessions = await _dbContext.TrainingSessions
                .Include(s => s.Sport)
                .Where(s => s.CoachID == coachId &&
                            s.Status == SessionStatus.Scheduled &&
                            s.SessionDate >= DateTime.UtcNow.Date)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.Start_Time)
                .Take(10)
                .Select(s => new
                {
                    s.SessionID,
                    s.SessionDate,
                    s.SessionType,
                    s.Location,
                    s.Start_Time,
                    s.End_Time,
                    s.MaxParticipants,
                    SportName = s.Sport.Name,
                    Price = _dbContext.CoachSports
                        .Where(cs => cs.CoachID == s.CoachID && cs.SportID == s.SportID)
                        .Select(cs => cs.PricePerSession).FirstOrDefault(),
                    AvailableSlots = s.MaxParticipants -
                        _dbContext.ClientSessions.Count(cs => cs.SessionID == s.SessionID)
                }).ToListAsync();

            var reviews = await _dbContext.Reviews
                .Include(r => r.Client)
                .Where(r => r.CoachID == coachId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .Select(r => new
                {
                    r.ReviewID,
                    r.Rating,
                    r.Comment,
                    r.CoachResponse,
                    r.CreatedAt,
                    ClientName = r.Client.F_name + " " + r.Client.L_name
                }).ToListAsync();

            var result = new
            {
                coach.CoachID,
                Name = coach.F_name + " " + coach.L_name,
                coach.Bio,
                coach.ExperienceYears,
                coach.AvgRating,
                coach.Gender,
                coach.URL,
                coach.CertificateUrl,
                verificationStatus = coach.VerificationStatus.ToString(),
                Email = coach.User.Email,
                PhoneNumber = coach.User.PhoneNumber,
                AvailableDays = string.IsNullOrWhiteSpace(coach.AvailabilityStatus)
                    ? new List<string>()
                    : coach.AvailabilityStatus.Split(',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Sports = coach.CoachSports.Select(cs => new
                {
                    sportID = cs.SportID,
                    cs.Sport.Name,
                    cs.Description,
                    cs.PricePerSession,
                    cs.ExperienceYears
                }).ToList(),
                Locations = coach.CoachLocations.Select(cl => cl.WorkingLocation).ToList(),
                UpcomingSessions = upcomingSessions,
                RecentReviews = reviews,
                TotalReviews = await _dbContext.Reviews.CountAsync(r => r.CoachID == coachId)
            };

            return (true, result);
        }
    }
}