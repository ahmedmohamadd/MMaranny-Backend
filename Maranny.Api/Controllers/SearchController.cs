using Maranny.Application.DTOs.Search;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public SearchController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("coaches")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchCoaches([FromQuery] CoachSearchDto dto)
        {
            var query = _dbContext.Coaches
                .Include(c => c.User)
                .Include(c => c.CoachLocations)
                .Include(c => c.CoachSports)
                    .ThenInclude(cs => cs.Sport)
                .Where(c => !c.User.IsBlocked);

            if (dto.VerifiedOnly ?? true)
            {
                query = query.Where(c => c.VerificationStatus == VerificationStatus.Approved);
            }

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var nameLower = dto.Name.ToLower();
                query = query.Where(c =>
                    (c.F_name + " " + c.L_name).ToLower().Contains(nameLower) ||
                    c.F_name.ToLower().Contains(nameLower) ||
                    c.L_name.ToLower().Contains(nameLower));
            }

            if (dto.SportID.HasValue)
            {
                query = query.Where(c => c.CoachSports.Any(cs => cs.SportID == dto.SportID.Value));
            }

            if (!string.IsNullOrWhiteSpace(dto.City))
            {
                var cityLower = dto.City.ToLower();
                query = query.Where(c => c.CoachLocations.Any(cl => cl.WorkingLocation.ToLower().Contains(cityLower)));
            }

            if (dto.MinRating.HasValue)
            {
                query = query.Where(c => c.AvgRating >= dto.MinRating.Value);
            }

            if (dto.MinExperience.HasValue)
            {
                query = query.Where(c => c.ExperienceYears >= dto.MinExperience.Value);
            }

            if (!string.IsNullOrWhiteSpace(dto.Gender) && Enum.TryParse<Gender>(dto.Gender, out var gender))
            {
                query = query.Where(c => c.Gender == gender);
            }

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
                    c.Age,
                    c.URL,
                    c.CertificateUrl,
                    c.CertificateImageUrl,
                    verificationStatus = c.VerificationStatus.ToString(),
                    Email = c.User.Email,
                    PhoneNumber = c.User.PhoneNumber,
                    AvailableDays = ParseAvailability(c.AvailabilityStatus).AvailableDays,
                    AvailableHours = ParseAvailability(c.AvailabilityStatus).AvailableHours,
                    DayHourSlots = ParseAvailability(c.AvailabilityStatus).DayHourSlots,
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
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page = dto.Page,
                pageSize = dto.PageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)dto.PageSize),
                coaches
            });
        }

        [HttpGet("coaches/{coachId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCoachDetails(int coachId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                _dbContext.UserInteractions.Add(new Core.Entities.UserInteraction
                {
                    UserId = userId,
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
                .Include(c => c.CoachSports)
                    .ThenInclude(cs => cs.Sport)
                .FirstOrDefaultAsync(c => c.CoachID == coachId);

            if (coach == null)
            {
                return NotFound(new { error = "Coach not found" });
            }

            var upcomingSessions = await _dbContext.TrainingSessions
                .Include(s => s.Sport)
                .Where(s => s.CoachID == coachId &&
                           s.Status == SessionStatus.Scheduled &&
                           s.SessionDate >= DateTime.UtcNow.Date)
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.Start_Time)
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
                        .Select(cs => cs.PricePerSession)
                        .FirstOrDefault(),
                    AvailableSlots = s.MaxParticipants.HasValue
                        ? s.MaxParticipants.Value - _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                        : (int?)null
                })
                .ToListAsync();

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
                })
                .ToListAsync();

            return Ok(new
            {
                coach.CoachID,
                Name = coach.F_name + " " + coach.L_name,
                coach.Bio,
                coach.ExperienceYears,
                coach.AvgRating,
                Gender = coach.Gender.HasValue ? coach.Gender.ToString() : null,
                coach.Age,
                coach.URL,
                coach.CertificateUrl,
                coach.CertificateImageUrl,
                verificationStatus = coach.VerificationStatus.ToString(),
                Email = coach.User.Email,
                PhoneNumber = coach.User.PhoneNumber,
                AvailableDays = ParseAvailability(coach.AvailabilityStatus).AvailableDays,
                AvailableHours = ParseAvailability(coach.AvailabilityStatus).AvailableHours,
                DayHourSlots = ParseAvailability(coach.AvailabilityStatus).DayHourSlots,
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
            });
        }

        private static AvailabilityPayload ParseAvailability(string? availabilityStatus)
        {
            if (string.IsNullOrWhiteSpace(availabilityStatus))
            {
                return new AvailabilityPayload();
            }

            var trimmed = availabilityStatus.Trim();
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<AvailabilityPayload>(trimmed) ?? new AvailabilityPayload();
                    payload.AvailableDays ??= new List<string>();
                    payload.AvailableHours ??= new List<string>();
                    payload.DayHourSlots ??= new List<DayHourSlot>();
                    if (payload.DayHourSlots.Count == 0 && payload.AvailableDays.Count > 0)
                    {
                        payload.DayHourSlots = payload.AvailableDays.Select(day => new DayHourSlot
                        {
                            Day = day,
                            Hours = payload.AvailableHours.ToList()
                        }).ToList();
                    }
                    return payload;
                }
                catch
                {
                    return new AvailabilityPayload();
                }
            }

            var legacyDays = trimmed
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AvailabilityPayload
            {
                AvailableDays = legacyDays,
                DayHourSlots = legacyDays.Select(day => new DayHourSlot
                {
                    Day = day,
                    Hours = new List<string>()
                }).ToList()
            };
        }

        private sealed class DayHourSlot
        {
            public string Day { get; set; } = string.Empty;
            public List<string> Hours { get; set; } = new();
        }

        private sealed class AvailabilityPayload
        {
            public List<string> AvailableDays { get; set; } = new();
            public List<string> AvailableHours { get; set; } = new();
            public List<DayHourSlot> DayHourSlots { get; set; } = new();
        }
    }
}
