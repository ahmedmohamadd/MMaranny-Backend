using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Core.Entities;
using Maranny.Core.Enums;

namespace Maranny.Application.Features.Reviews.SubmitReview
{
    public sealed class SubmitReviewUseCase : ISubmitReviewUseCase
    {
        private readonly IClientRepository _clients;
        private readonly ITrainingSessionRepository _sessions;
        private readonly IBookingRepository _bookings;
        private readonly IReviewRepository _reviews;
        private readonly ICoachRepository _coaches;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;

        public SubmitReviewUseCase(
            IClientRepository clients,
            ITrainingSessionRepository sessions,
            IBookingRepository bookings,
            IReviewRepository reviews,
            ICoachRepository coaches,
            IUnitOfWork unitOfWork,
            IClock clock)
        {
            _clients = clients;
            _sessions = sessions;
            _bookings = bookings;
            _reviews = reviews;
            _coaches = coaches;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<Result<object>> ExecuteAsync(SubmitReviewCommand command)
        {
            var client = await _clients.GetByUserIdAsync(command.UserId);
            if (client == null)
            {
                return Failure("Client profile not found");
            }

            var session = await _sessions.GetByIdAsync(command.Review.SessionID);
            if (session == null)
            {
                return Failure("Session not found");
            }

            if (session.CoachID != command.Review.CoachID)
            {
                return Failure("Session does not belong to this coach");
            }

            if (!await _bookings.ClientHasSessionAsync(client.ClientID, command.Review.SessionID))
            {
                return Failure("You did not attend this session");
            }

            if (session.Status != SessionStatus.Completed)
            {
                return Failure("Cannot review a session that is not completed");
            }

            if (await _reviews.ClientReviewedSessionAsync(client.ClientID, command.Review.SessionID))
            {
                return Failure("You have already reviewed this session");
            }

            var review = new Review
            {
                SessionID = command.Review.SessionID,
                ClientID = client.ClientID,
                CoachID = command.Review.CoachID,
                Rating = command.Review.Rating,
                Comment = command.Review.Comment,
                CreatedAt = _clock.UtcNow
            };

            await _reviews.AddAsync(review);
            await _unitOfWork.SaveChangesAsync();

            var coach = await _coaches.GetByIdAsync(command.Review.CoachID);
            if (coach != null)
            {
                coach.AvgRating = await _reviews.GetCoachAverageRatingAsync(command.Review.CoachID);
                await _unitOfWork.SaveChangesAsync();
            }

            return Result<object>.Success(new
            {
                message = "Review submitted successfully",
                data = new { reviewId = review.ReviewID }
            });
        }

        private static Result<object> Failure(string message)
        {
            return Result<object>.Failure(new Error("Review.SubmitFailed", message, ErrorType.Failure));
        }
    }
}
