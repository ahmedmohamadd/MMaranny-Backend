using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Application.Interfaces;
using Maranny.Core.Enums;
using Maranny.Core.Policies;

namespace Maranny.Application.Features.Bookings.CoachCancelSession
{
    public sealed class CoachCancelSessionUseCase : ICoachCancelSessionUseCase
    {
        private readonly ICoachRepository _coaches;
        private readonly ITrainingSessionRepository _sessions;
        private readonly IBookingRepository _bookings;
        private readonly IPaymentRepository _payments;
        private readonly IClientRepository _clients;
        private readonly IPaymentService _paymentService;
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;

        public CoachCancelSessionUseCase(
            ICoachRepository coaches,
            ITrainingSessionRepository sessions,
            IBookingRepository bookings,
            IPaymentRepository payments,
            IClientRepository clients,
            IPaymentService paymentService,
            INotificationService notificationService,
            IUnitOfWork unitOfWork,
            IClock clock)
        {
            _coaches = coaches;
            _sessions = sessions;
            _bookings = bookings;
            _payments = payments;
            _clients = clients;
            _paymentService = paymentService;
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<Result<object>> ExecuteAsync(CoachCancelSessionCommand command)
        {
            var coach = await _coaches.GetByUserIdAsync(command.UserId);
            if (coach == null)
            {
                return Failure("Coach profile not found");
            }

            var session = await _sessions.GetByIdForBookingAsync(command.SessionId);
            if (session == null)
            {
                return Failure("Session not found");
            }

            if (session.CoachID != coach.CoachID)
            {
                return Failure("Forbidden");
            }

            if (session.Status == SessionStatus.Cancelled)
            {
                return Failure("Session is already cancelled");
            }

            if (session.Status == SessionStatus.Completed)
            {
                return Failure("Cannot cancel completed session");
            }

            var sessionStart = session.SessionDate.Add(session.Start_Time);
            if (!BookingPolicy.CanCoachCancelSession(session.Status, sessionStart, _clock.UtcNow))
            {
                return Failure("Cannot cancel a session that has already started");
            }

            var bookings = await _bookings.GetActiveBySessionIdAsync(command.SessionId);

            var refundedCount = 0;
            decimal totalRefunded = 0;

            foreach (var booking in bookings)
            {
                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = _clock.UtcNow;
                booking.CancelledByCoach = true;
                booking.CancellationReason = command.Reason ?? "Cancelled by coach";

                var payment = await _payments.GetCompletedByBookingIdAsync(booking.BookingID);
                if (payment != null)
                {
                    await _paymentService.ProcessRefundAsync(
                        payment.PaymentID,
                        payment.Amount,
                        "Coach cancelled. Full refund.");

                    refundedCount++;
                    totalRefunded += payment.Amount;

                    var clientUserId = await _clients.GetUserIdByClientIdAsync(booking.ClientID);
                    if (clientUserId != 0)
                    {
                        await _notificationService.SendNotificationAsync(
                            clientUserId,
                            "Session Cancelled by Coach",
                            $"Your session on {session.SessionDate:MMM dd} was cancelled. Full refund of {payment.Amount:F2} EGP.",
                            NotificationType.BookingCancellation);
                    }
                }
            }

            session.Status = SessionStatus.Cancelled;
            await _unitOfWork.SaveChangesAsync();

            return Result<object>.Success(new
            {
                message = "Session cancelled successfully",
                data = new
                {
                    bookingsCancelled = bookings.Count,
                    refundsIssued = refundedCount,
                    totalRefundAmount = totalRefunded
                }
            });
        }

        private static Result<object> Failure(string message)
        {
            return Result<object>.Failure(new Error(
                "Booking.CoachCancelSessionFailed",
                message,
                ErrorType.Failure));
        }
    }
}
