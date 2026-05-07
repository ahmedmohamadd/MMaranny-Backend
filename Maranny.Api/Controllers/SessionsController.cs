using Maranny.Application.DTOs.Sessions;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/sessions")]
    public class SessionsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public SessionsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
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
                .Where(day => !string.IsNullOrWhiteSpace(day))
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

        private static int? MapDayNameToDayOfWeekNumber(string dayName)
        {
            if (Enum.TryParse<DayOfWeek>(dayName, true, out var parsedDay))
            {
                return (int)parsedDay;
            }

            return dayName.Trim().ToLowerInvariant() switch
            {
                "saturday" => (int)DayOfWeek.Saturday,
                "sunday" => (int)DayOfWeek.Sunday,
                "monday" => (int)DayOfWeek.Monday,
                "tuesday" => (int)DayOfWeek.Tuesday,
                "wednesday" => (int)DayOfWeek.Wednesday,
                "thursday" => (int)DayOfWeek.Thursday,
                "friday" => (int)DayOfWeek.Friday,
                _ => null
            };
        }

        private static List<object> BuildUpcomingAvailabilityDates(AvailabilityPayload availability, int numberOfOccurrences = 14)
        {
            var slotLookup = availability.DayHourSlots
                .Where(slot => !string.IsNullOrWhiteSpace(slot.Day))
                .GroupBy(slot => slot.Day.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.SelectMany(slot => slot.Hours ?? new List<string>())
                        .Where(hour => !string.IsNullOrWhiteSpace(hour))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var dayNumbers = availability.AvailableDays
                .Select(MapDayNameToDayOfWeekNumber)
                .Where(day => day.HasValue)
                .Select(day => day!.Value)
                .Distinct()
                .ToHashSet();

            var results = new List<object>();
            if (dayNumbers.Count == 0)
            {
                return results;
            }

            var date = DateTime.UtcNow.Date;
            while (results.Count < numberOfOccurrences)
            {
                if (dayNumbers.Contains((int)date.DayOfWeek))
                {
                    var dayName = date.DayOfWeek.ToString();
                    var hours = slotLookup.TryGetValue(dayName, out var slotHours) && slotHours.Any()
                        ? slotHours
                        : availability.AvailableHours.ToList();

                    results.Add(new
                    {
                        date,
                        dayName,
                        formattedDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        availableHours = hours
                    });
                }

                date = date.AddDays(1);
            }

            return results;
        }

        [HttpPost]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CreateSession(CreateSessionDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            if (coach.VerificationStatus != VerificationStatus.Verified &&
                coach.VerificationStatus != VerificationStatus.Approved)
            {
                return BadRequest(new { error = "Coach must be verified before creating sessions" });
            }

            if (dto.SessionDate.Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new { error = "Cannot create session in the past" });
            }

            if (dto.End_Time <= dto.Start_Time)
            {
                return BadRequest(new { error = "End time must be after start time" });
            }

            var sport = await _dbContext.Sports.FindAsync(dto.SportID);
            if (sport == null)
            {
                return NotFound(new { error = "Sport not found" });
            }

            var overlappingSession = await _dbContext.TrainingSessions
                .Where(s => s.CoachID == coach.CoachID &&
                           s.SessionDate.Date == dto.SessionDate.Date &&
                           s.Status != SessionStatus.Cancelled &&
                           ((dto.Start_Time >= s.Start_Time && dto.Start_Time < s.End_Time) ||
                            (dto.End_Time > s.Start_Time && dto.End_Time <= s.End_Time) ||
                            (dto.Start_Time <= s.Start_Time && dto.End_Time >= s.End_Time)))
                .FirstOrDefaultAsync();

            if (overlappingSession != null)
            {
                return BadRequest(new { error = "You have an overlapping session at this time" });
            }

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
                Status = SessionStatus.Scheduled,
            };

            _dbContext.TrainingSessions.Add(session);
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Session created successfully",
                sessionId = session.SessionID
            });
        }

        [HttpGet("my")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GetMySessions(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
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
                    BookedCount = _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)),
                    AvailableSlots = s.MaxParticipants.HasValue
                        ? s.MaxParticipants.Value - _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                        : (int?)null
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                sessions
            });
        }

        [HttpGet("availability/{coachId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCoachAvailability(int coachId)
        {
            var coach = await _dbContext.Coaches
                .Include(c => c.CoachSports)
                    .ThenInclude(cs => cs.Sport)
                .Include(c => c.CoachLocations)
                .FirstOrDefaultAsync(c => c.CoachID == coachId);

            if (coach == null)
            {
                return NotFound(new { error = "Coach not found" });
            }

            var availability = ParseAvailability(coach.AvailabilityStatus);
            var upcomingAvailableDates = BuildUpcomingAvailabilityDates(availability);

            var sessions = await _dbContext.TrainingSessions
                .Include(s => s.Sport)
                .Where(s => s.CoachID == coachId &&
                            s.Status == SessionStatus.Scheduled &&
                            s.SessionDate >= DateTime.UtcNow.Date)
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.Start_Time)
                .Select(s => new
                {
                    s.SessionID,
                    s.SessionDate,
                    Status = s.Status.ToString(),
                    s.Start_Time,
                    s.End_Time,
                    s.SessionType,
                    s.Location,
                    s.MaxParticipants,
                    PendingBookings = _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && b.Status == BookingStatus.Pending),
                    ConfirmedBookings = _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && b.Status == BookingStatus.Confirmed),
                    ReservationStatus = _dbContext.Bookings.Any(b => b.SessionID == s.SessionID && b.Status == BookingStatus.Confirmed)
                        ? "Confirmed"
                        : _dbContext.Bookings.Any(b => b.SessionID == s.SessionID && b.Status == BookingStatus.Pending)
                            ? "Pending"
                            : "Free",
                    SportName = s.Sport.Name,
                    SportID = s.SportID,
                    Price = _dbContext.CoachSports
                        .Where(cs => cs.CoachID == s.CoachID && cs.SportID == s.SportID)
                        .Select(cs => cs.PricePerSession)
                        .FirstOrDefault(),
                    AvailableSlots = s.MaxParticipants.HasValue
                        ? s.MaxParticipants.Value - _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                        : (int?)null
                })
                .ToListAsync();

            var weeklySlotStatuses = sessions
                .Select(s => new
                {
                    dayName = s.SessionDate.DayOfWeek.ToString(),
                    date = s.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    hour = DateTime.Today.Add(s.Start_Time).ToString("h:mm tt", CultureInfo.InvariantCulture),
                    reservationStatus = s.ReservationStatus,
                    pendingBookings = s.PendingBookings,
                    confirmedBookings = s.ConfirmedBookings,
                    availableSlots = s.AvailableSlots,
                    sessionId = s.SessionID
                })
                .GroupBy(
                    slot => $"{slot.dayName.Trim().ToLowerInvariant()}|{slot.hour.Trim().ToUpperInvariant()}")
                .Select(group =>
                {
                    var confirmed = group.FirstOrDefault(slot => string.Equals(slot.reservationStatus, "Confirmed", StringComparison.OrdinalIgnoreCase));
                    if (confirmed != null)
                    {
                        return confirmed;
                    }

                    var pending = group.FirstOrDefault(slot => string.Equals(slot.reservationStatus, "Pending", StringComparison.OrdinalIgnoreCase));
                    if (pending != null)
                    {
                        return pending;
                    }

                    return group.First();
                })
                .ToList();

            return Ok(new
            {
                coachId = coach.CoachID,
                availableDays = availability.AvailableDays,
                availableHours = availability.AvailableHours,
                dayHourSlots = availability.DayHourSlots,
                upcomingAvailableDates,
                weeklySlotStatuses,
                locations = coach.CoachLocations.Select(cl => cl.WorkingLocation).ToList(),
                sports = coach.CoachSports.Select(cs => new
                {
                    sportID = cs.SportID,
                    cs.Sport.Name,
                    cs.PricePerSession
                }).ToList(),
                hasProfileAvailability = availability.AvailableDays.Any(),
                hasProfileAvailableHours = availability.AvailableHours.Any() || availability.DayHourSlots.Any(slot => slot.Hours.Any()),
                hasRealSessions = sessions.Any(),
                sessions
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableSessions(
            [FromQuery] int? coachId = null,
            [FromQuery] int? sportId = null,
            [FromQuery] DateTime? date = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _dbContext.TrainingSessions
                .Include(s => s.Sport)
                .Include(s => s.Coach)
                .Where(s => s.Status == SessionStatus.Scheduled &&
                           s.SessionDate >= DateTime.UtcNow.Date);

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
                _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)) < s.MaxParticipants.Value);

            var totalCount = await query.CountAsync();

            var availability = new AvailabilityPayload();
            List<object> upcomingAvailableDates = new();
            if (coachId.HasValue)
            {
                var coachAvailability = await _dbContext.Coaches
                    .Where(c => c.CoachID == coachId.Value)
                    .Select(c => c.AvailabilityStatus)
                    .FirstOrDefaultAsync();

                availability = ParseAvailability(coachAvailability);
                upcomingAvailableDates = BuildUpcomingAvailabilityDates(availability);
            }

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
                    Status = s.Status.ToString(),
                    PendingBookings = _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && b.Status == BookingStatus.Pending),
                    ConfirmedBookings = _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && b.Status == BookingStatus.Confirmed),
                    ReservationStatus = _dbContext.Bookings.Any(b => b.SessionID == s.SessionID && b.Status == BookingStatus.Confirmed)
                        ? "Confirmed"
                        : _dbContext.Bookings.Any(b => b.SessionID == s.SessionID && b.Status == BookingStatus.Pending)
                            ? "Pending"
                            : "Free",
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
                    AvailableSlots = s.MaxParticipants.HasValue
                        ? s.MaxParticipants.Value - _dbContext.Bookings.Count(b => b.SessionID == s.SessionID && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                        : (int?)null
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                availableDays = availability.AvailableDays,
                availableHours = availability.AvailableHours,
                dayHourSlots = availability.DayHourSlots,
                upcomingAvailableDates,
                hasProfileAvailability = availability.AvailableDays.Any(),
                hasProfileAvailableHours = availability.AvailableHours.Any() || availability.DayHourSlots.Any(slot => slot.Hours.Any()),
                hasRealSessions = totalCount > 0,
                sessions
            });
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

        [HttpPut("{sessionId}")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> UpdateSession(int sessionId, UpdateSessionDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            var session = await _dbContext.TrainingSessions.FindAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }

            if (session.CoachID != coach.CoachID)
            {
                return Forbid();
            }

            if (dto.SessionDate.HasValue)
                session.SessionDate = dto.SessionDate.Value;

            if (!string.IsNullOrEmpty(dto.SessionType))
                session.SessionType = dto.SessionType;

            if (!string.IsNullOrEmpty(dto.Location))
                session.Location = dto.Location;

            if (dto.MaxParticipants.HasValue)
                session.MaxParticipants = dto.MaxParticipants.Value;

            if (dto.Start_Time.HasValue)
                session.Start_Time = dto.Start_Time.Value;

            if (dto.End_Time.HasValue)
                session.End_Time = dto.End_Time.Value;

            if (!string.IsNullOrEmpty(dto.Status) && Enum.TryParse<SessionStatus>(dto.Status, out var status))
                session.Status = status;

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Session updated successfully" });
        }

        [HttpDelete("{sessionId}")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CancelSession(int sessionId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            var session = await _dbContext.TrainingSessions.FindAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }

            if (session.CoachID != coach.CoachID)
            {
                return Forbid();
            }

            session.Status = SessionStatus.Cancelled;
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Session cancelled successfully" });
        }
    }
}
