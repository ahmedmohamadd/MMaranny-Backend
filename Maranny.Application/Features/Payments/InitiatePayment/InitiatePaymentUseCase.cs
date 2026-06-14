using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Application.Interfaces;
using Maranny.Core.Enums;

namespace Maranny.Application.Features.Payments.InitiatePayment
{
    public sealed class InitiatePaymentUseCase : IInitiatePaymentUseCase
    {
        private readonly IClientRepository _clients;
        private readonly IBookingRepository _bookings;
        private readonly ICoachSportRepository _coachSports;
        private readonly IPaymentRepository _payments;
        private readonly IPaymentService _paymentService;

        public InitiatePaymentUseCase(
            IClientRepository clients,
            IBookingRepository bookings,
            ICoachSportRepository coachSports,
            IPaymentRepository payments,
            IPaymentService paymentService)
        {
            _clients = clients;
            _bookings = bookings;
            _coachSports = coachSports;
            _payments = payments;
            _paymentService = paymentService;
        }

        public async Task<Result<object>> ExecuteAsync(InitiatePaymentCommand command)
        {
            var client = await _clients.GetByUserIdAsync(command.UserId);
            if (client == null)
            {
                return Failure("Client profile not found");
            }

            var booking = await _bookings.GetByIdWithSessionAndCoachAsync(command.Payment.BookingID);
            if (booking == null)
            {
                return Failure("Booking not found");
            }

            if (booking.ClientID != client.ClientID)
            {
                return Failure("Forbidden");
            }

            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
            {
                return Failure("Payment cannot be initiated for this booking");
            }

            var normalizedMethod = command.Payment.Method?.Trim();
            if (!string.Equals(normalizedMethod, "Card", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedMethod, "Wallet", StringComparison.OrdinalIgnoreCase))
            {
                return Failure("Only Card and Wallet payment methods are supported");
            }

            var expectedAmount = await _coachSports.GetSessionPriceAsync(
                booking.TrainingSession.CoachID,
                booking.TrainingSession.SportID);

            if (!expectedAmount.HasValue || expectedAmount.Value <= 0)
            {
                return Failure("Session price is not configured");
            }

            if (command.Payment.Amount != expectedAmount.Value)
            {
                return Failure($"Amount mismatch. Expected: {expectedAmount.Value}");
            }

            var existingPayment = await _payments.GetByBookingIdAsync(command.Payment.BookingID);
            if (existingPayment != null)
            {
                if (existingPayment.Status == PaymentStatus.Completed)
                {
                    return Failure("Payment already completed for this booking");
                }

                if (existingPayment.Status == PaymentStatus.Pending)
                {
                    return Failure("Payment already initiated. Please complete existing payment.");
                }
            }

            try
            {
                var payment = await _paymentService.InitiatePaymentAsync(
                    command.Payment.BookingID,
                    expectedAmount.Value,
                    NormalizeMethod(normalizedMethod!),
                    client.ClientID);

                var paymentUrl = await _paymentService.GeneratePaymentUrlAsync(payment);

                return Result<object>.Success(new
                {
                    message = "Payment initiated successfully",
                    data = new
                    {
                        paymentId = payment.PaymentID,
                        paymentUrl,
                        amount = payment.Amount,
                        platformFee = payment.PlatformFee,
                        bookingStatus = booking.Status.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                return Failure($"Failed to initiate payment: {ex.Message}");
            }
        }

        private static string NormalizeMethod(string method)
        {
            return string.Equals(method, "wallet", StringComparison.OrdinalIgnoreCase) ? "Wallet" : "Card";
        }

        private static Result<object> Failure(string message)
        {
            return Result<object>.Failure(new Error("Payment.InitiateFailed", message, ErrorType.Failure));
        }
    }
}
