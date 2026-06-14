using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Application.Interfaces;
using Maranny.Core.Enums;

namespace Maranny.Application.Features.Bookings.DeclineBooking
{
    public sealed class DeclineBookingUseCase : IDeclineBookingUseCase
    {
        private readonly ICoachRepository _coaches;
        private readonly IBookingRepository _bookings;
        private readonly IClientRepository _clients;
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;

        public DeclineBookingUseCase(
            ICoachRepository coaches,
            IBookingRepository bookings,
            IClientRepository clients,
            INotificationService notificationService,
            IUnitOfWork unitOfWork,
            IClock clock)
        {
            _coaches = coaches;
            _bookings = bookings;
            _clients = clients;
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<Result<string>> ExecuteAsync(DeclineBookingCommand command)
        {
            var coach = await _coaches.GetByUserIdAsync(command.UserId);
            if (coach == null)
            {
                return Failure("Coach profile not found");
            }

            var booking = await _bookings.GetByIdWithSessionAsync(command.BookingId);
            if (booking == null)
            {
                return Failure("Booking not found");
            }

            if (booking.TrainingSession.CoachID != coach.CoachID)
            {
                return Failure("Forbidden");
            }

            if (booking.Status != BookingStatus.Pending)
            {
                return Failure("Only pending bookings can be declined");
            }

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = _clock.UtcNow;
            booking.CancelledByCoach = true;
            booking.CancellationReason = command.Action?.Reason ?? "Declined by coach";
            await _unitOfWork.SaveChangesAsync();

            var clientUserId = await _clients.GetUserIdByClientIdAsync(booking.ClientID);
            if (clientUserId != 0)
            {
                await _notificationService.SendNotificationAsync(
                    clientUserId,
                    "Booking Declined",
                    $"Your booking for {booking.TrainingSession.SessionDate:MMM dd} was declined.",
                    NotificationType.BookingCancellation);
            }

            return Result<string>.Success("Booking declined successfully");
        }

        private static Result<string> Failure(string message)
        {
            return Result<string>.Failure(new Error("Booking.DeclineFailed", message, ErrorType.Failure));
        }
    }
}
