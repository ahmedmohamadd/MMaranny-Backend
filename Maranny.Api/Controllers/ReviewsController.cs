using Maranny.Application.DTOs.Reviews;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public ReviewsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> SubmitReview(SubmitReviewDto dto)
        {
            // Get current user (client)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            // Get client ID
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return NotFound(new { error = "Client profile not found" });
            }

            // Verify session exists
            var session = await _dbContext.TrainingSessions
                .FirstOrDefaultAsync(s => s.SessionID == dto.SessionID);

            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }

            // Verify session belongs to the specified coach
            if (session.CoachID != dto.CoachID)
            {
                return BadRequest(new { error = "Session does not belong to this coach" });
            }

            // Verify client attended this session
            var clientSession = await _dbContext.ClientSessions
                .FirstOrDefaultAsync(cs => cs.ClientID == client.ClientID && cs.SessionID == dto.SessionID);

            if (clientSession == null)
            {
                return BadRequest(new { error = "You did not attend this session" });
            }

            var booking = await _dbContext.Bookings
                .FirstOrDefaultAsync(b => b.ClientID == client.ClientID && b.SessionID == dto.SessionID);

            var sessionHasEnded = HasSessionEndedInCairo(session);
            var bookingIsConfirmedOrCompleted =
                booking?.Status == BookingStatus.Confirmed ||
                booking?.Status == BookingStatus.Completed;

            // Reviews are allowed after the confirmed session time has passed.
            // Some mobile-created sessions remain Scheduled until the first review, so
            // relying only on SessionStatus.Completed makes valid past sessions fail.
            if (session.Status != SessionStatus.Completed &&
                (!sessionHasEnded || !bookingIsConfirmedOrCompleted))
            {
                return BadRequest(new { error = "You can review this session after it is completed" });
            }

            // Check if review already exists (one review per client per coach per session)
            var existingReview = await _dbContext.Reviews
                .FirstOrDefaultAsync(r => r.ClientID == client.ClientID && r.SessionID == dto.SessionID);

            if (existingReview != null)
            {
                return BadRequest(new { error = "You have already reviewed this session" });
            }

            // Create review
            var review = new Review
            {
                SessionID = dto.SessionID,
                ClientID = client.ClientID,
                CoachID = dto.CoachID,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Reviews.Add(review);

            if (session.Status != SessionStatus.Completed)
            {
                session.Status = SessionStatus.Completed;
            }

            if (booking != null && booking.Status != BookingStatus.Completed)
            {
                booking.Status = BookingStatus.Completed;
            }

            await _dbContext.SaveChangesAsync();

            // Update coach average rating
            await UpdateCoachAverageRating(dto.CoachID);

            return Ok(new
            {
                message = "Review submitted successfully",
                reviewId = review.ReviewID
            });
        }

        private static bool HasSessionEndedInCairo(TrainingSession session)
        {
            var sessionEnd = session.SessionDate.Date + session.End_Time;
            var cairoNow = GetCairoNow();
            return sessionEnd <= cairoNow;
        }

        private static DateTime GetCairoNow()
        {
            foreach (var timeZoneId in new[] { "Egypt Standard Time", "Africa/Cairo" })
            {
                try
                {
                    var cairoTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cairoTimeZone);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return DateTime.UtcNow.AddHours(3);
        }

        [HttpGet("my-coach-reviews")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GetMyCoachReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
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

            return await BuildCoachReviewsResponse(coach.CoachID, page, pageSize);
        }

        [HttpGet("coach/{coachId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCoachReviews(int coachId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // Verify coach exists
            var coach = await _dbContext.Coaches.FindAsync(coachId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach not found" });
            }

            return await BuildCoachReviewsResponse(coachId, page, pageSize);
        }

        private async Task<IActionResult> BuildCoachReviewsResponse(int coachId, int page, int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            // Get reviews with pagination
            var query = _dbContext.Reviews
                .Include(r => r.Client)
                .Include(r => r.TrainingSession)
                    .ThenInclude(s => s.Sport)
                .Where(r => r.CoachID == coachId)
                .OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var averageRating = await query.AverageAsync(r => (decimal?)r.Rating) ?? 0;

            var reviews = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    reviewId = r.ReviewID,
                    rating = r.Rating,
                    comment = r.Comment,
                    coachResponse = r.CoachResponse,
                    responseDate = r.ResponseDate,
                    createdAt = r.CreatedAt,
                    sessionId = r.SessionID,
                    coachId = r.CoachID,
                    clientId = r.ClientID,
                    sportName = r.TrainingSession.Sport.Name,
                    client = new
                    {
                        id = r.Client.ClientID,
                        clientID = r.Client.ClientID,
                        name = r.Client.F_name + " " + r.Client.L_name,
                        profilePicture = r.Client.URL
                    }
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount = totalCount,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                averageRating = averageRating,
                reviews = reviews
            });
        }

        [HttpPut("{reviewId}/response")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> RespondToReview(int reviewId, CoachResponseDto dto)
        {
            // Get current user (coach)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            // Get coach ID
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            // Get review
            var review = await _dbContext.Reviews.FindAsync(reviewId);
            if (review == null)
            {
                return NotFound(new { error = "Review not found" });
            }

            // Verify review is for this coach
            if (review.CoachID != coach.CoachID)
            {
                return Forbid();
            }

            // Check if coach already responded
            if (!string.IsNullOrEmpty(review.CoachResponse))
            {
                return BadRequest(new { error = "You have already responded to this review" });
            }

            // Add response
            review.CoachResponse = dto.Response;
            review.ResponseDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Response added successfully" });
        }

        // Helper method to update coach average rating
        private async Task UpdateCoachAverageRating(int coachId)
        {
            var coach = await _dbContext.Coaches.FindAsync(coachId);
            if (coach == null) return;

            var averageRating = await _dbContext.Reviews
                .Where(r => r.CoachID == coachId)
                .AverageAsync(r => (decimal?)r.Rating);

            coach.AvgRating = averageRating ?? 0;
            await _dbContext.SaveChangesAsync();
        }
    }
}
