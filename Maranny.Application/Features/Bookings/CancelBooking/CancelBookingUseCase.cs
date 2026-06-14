using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Application.Abstractions.Notifications;
using Maranny.Application.Interfaces;
using Maranny.Core.Enums;
using Maranny.Core.Policies;

namespace Maranny.Application.Features.Bookings.CancelBooking
{
    public sealed class CancelBookingUseCase : ICancelBookingUseCase
    {
        private readonly IClientRepository _clients;
        private readonly IBookingRepository _bookings;
        private readonly IPaymentRepository _payments;
        private readonly ICoachRepository _coaches;
        private readonly IPaymentService _paymentService;
        private readonly INotificationGateway _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;

        public CancelBookingUseCase(
            IClientRepository clients,
            IBookingRepository bookings,
            IPaymentRepository payments,
            ICoachRepository coaches,
            IPaymentService paymentService,
            INotificationGateway notificationService,
            IUnitOfWork unitOfWork,
            IClock clock)
        {
            _clients = clients;
            _bookings = bookings;
            _payments = payments;
            _coaches = coaches;
            _paymentService = paymentService;
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<Result<object>> ExecuteAsync(CancelBookingCommand command)
        {
            var client = await _clients.GetByUserIdAsync(command.UserId);
            if (client == null)
            {
                return Failure("Client profile not found");
            }

            var booking = await _bookings.GetByIdWithSessionAsync(command.BookingId);
            if (booking == null)
            {
                return Failure("Booking not found");
            }

            if (booking.ClientID != client.ClientID)
            {
                return Failure("Forbidden");
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                return Failure("Booking is already cancelled");
            }

            if (booking.Status == BookingStatus.Completed)
            {
                return Failure("Cannot cancel completed booking");
            }

            var sessionStart = booking.TrainingSession.SessionDate.Add(booking.TrainingSession.Start_Time);
            if (!BookingPolicy.CanCancel(booking.Status, sessionStart, _clock.UtcNow))
            {
                return Failure("Cannot cancel a session that has already started");
            }

            var hoursUntil = (sessionStart - _clock.UtcNow).TotalHours;
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = _clock.UtcNow;
            booking.CancellationReason = command.Reason ?? "Cancelled by client";
            booking.CancelledByCoach = false;

            var refundMessage = "";
            var payment = await _payments.GetCompletedByBookingIdAsync(command.BookingId);
            if (payment != null)
            {
                var refundAmount = RefundPolicy.CalculateClientCancellationRefund(payment.Amount, hoursUntil);
                if (refundAmount > 0)
                {
                    await _paymentService.ProcessRefundAsync(
                        payment.PaymentID,
                        refundAmount,
                        $"Cancelled {hoursUntil:F1} hours before session. 90% refund.");

                    refundMessage = $"Refund of {refundAmount:F2} EGP will be processed (90%).";
                }
                else
                {
                    payment.RefundReason = $"Cancelled {hoursUntil:F1} hours before. No refund.";
                    refundMessage = "No refund. Cancellation within 24 hours.";
                }
            }

            await _unitOfWork.SaveChangesAsync();

            var coachUserId = await _coaches.GetUserIdByCoachIdAsync(booking.TrainingSession.CoachID);
            if (coachUserId != 0)
            {
                await _notificationService.SendNotificationAsync(
                    coachUserId,
                    "Booking Cancelled",
                    $"A booking for {booking.TrainingSession.SessionDate:MMM dd} was cancelled by the client.",
                    NotificationType.BookingCancellation);
            }

            return Result<object>.Success(new
            {
                message = "Booking cancelled successfully",
                data = new
                {
                    refundInfo = refundMessage,
                    hoursUntilSession = hoursUntil
                }
            });
        }

        private static Result<object> Failure(string message)
        {
            return Result<object>.Failure(new Error("Booking.CancelFailed", message, ErrorType.Failure));
        }
    }
}
