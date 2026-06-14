using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Application.Abstractions.Notifications;
using Maranny.Core.Enums;

namespace Maranny.Application.Features.Bookings.ApproveBooking
{
    public sealed class ApproveBookingUseCase : IApproveBookingUseCase
    {
        private readonly ICoachRepository _coaches;
        private readonly IBookingRepository _bookings;
        private readonly IClientRepository _clients;
        private readonly INotificationGateway _notificationService;
        private readonly IUnitOfWork _unitOfWork;

        public ApproveBookingUseCase(
            ICoachRepository coaches,
            IBookingRepository bookings,
            IClientRepository clients,
            INotificationGateway notificationService,
            IUnitOfWork unitOfWork)
        {
            _coaches = coaches;
            _bookings = bookings;
            _clients = clients;
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<string>> ExecuteAsync(ApproveBookingCommand command)
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
                return Failure("Only pending bookings can be approved");
            }

            booking.Status = BookingStatus.Confirmed;
            await _unitOfWork.SaveChangesAsync();

            var clientUserId = await _clients.GetUserIdByClientIdAsync(booking.ClientID);
            if (clientUserId != 0)
            {
                await _notificationService.SendNotificationAsync(
                    clientUserId,
                    "Booking Confirmed",
                    $"Your booking for {booking.TrainingSession.SessionDate:MMM dd} has been approved.",
                    NotificationType.BookingConfirmation);
            }

            return Result<string>.Success("Booking approved successfully");
        }

        private static Result<string> Failure(string message)
        {
            return Result<string>.Failure(new Error("Booking.ApproveFailed", message, ErrorType.Failure));
        }
    }
}
