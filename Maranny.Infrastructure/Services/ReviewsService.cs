using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Reviews;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Services
{
    public class ReviewsService : IReviewService
    {
        private readonly ApplicationDbContext _dbContext;

        public ReviewsService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(bool success, string message, object? data)> SubmitReviewAsync(int userId, SubmitReviewDto dto)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) return (false, "Client profile not found", null);

            var session = await _dbContext.TrainingSessions
                .FirstOrDefaultAsync(s => s.SessionID == dto.SessionID);
            if (session == null) return (false, "Session not found", null);

            if (session.CoachID != dto.CoachID)
                return (false, "Session does not belong to this coach", null);

            var clientSession = await _dbContext.ClientSessions
                .FirstOrDefaultAsync(cs => cs.ClientID == client.ClientID && cs.SessionID == dto.SessionID);
            if (clientSession == null) return (false, "You did not attend this session", null);

            if (session.Status != SessionStatus.Completed)
                return (false, "Cannot review a session that is not completed", null);

            var existingReview = await _dbContext.Reviews
                .FirstOrDefaultAsync(r => r.ClientID == client.ClientID && r.SessionID == dto.SessionID);
            if (existingReview != null) return (false, "You have already reviewed this session", null);

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
            await _dbContext.SaveChangesAsync();
            await UpdateCoachAverageRating(dto.CoachID);

            return (true, "Review submitted successfully", new { reviewId = review.ReviewID });
        }

        public async Task<(bool success, object? data)> GetCoachReviewsAsync(int coachId, int page, int pageSize)
        {
            var coach = await _dbContext.Coaches.FindAsync(coachId);
            if (coach == null) return (false, null);

            var query = _dbContext.Reviews
                .Include(r => r.Client)
                .Where(r => r.CoachID == coachId)
                .OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var reviews = await query
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new
                {
                    r.ReviewID,
                    r.Rating,
                    r.Comment,
                    r.CoachResponse,
                    r.ResponseDate,
                    r.CreatedAt,
                    client = new
                    {
                        name = r.Client.F_name + " " + r.Client.L_name,
                        profilePicture = r.Client.URL
                    }
                }).ToListAsync();

            return (true, new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                averageRating = coach.AvgRating,
                reviews
            });
        }

        public async Task<(bool success, string message)> RespondToReviewAsync(int userId, int reviewId, CoachResponseDto dto)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null) return (false, "Coach profile not found");

            var review = await _dbContext.Reviews.FindAsync(reviewId);
            if (review == null) return (false, "Review not found");
            if (review.CoachID != coach.CoachID) return (false, "Forbidden");
            if (!string.IsNullOrEmpty(review.CoachResponse))
                return (false, "You have already responded to this review");

            review.CoachResponse = dto.Response;
            review.ResponseDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return (true, "Response added successfully");
        }

        private async Task UpdateCoachAverageRating(int coachId)
        {
            var coach = await _dbContext.Coaches.FindAsync(coachId);
            if (coach == null) return;

            var avg = await _dbContext.Reviews
                .Where(r => r.CoachID == coachId)
                .AverageAsync(r => (decimal?)r.Rating);

            coach.AvgRating = avg ?? 0;
            await _dbContext.SaveChangesAsync();
        }
    }
}