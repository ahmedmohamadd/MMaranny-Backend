using Maranny.Application.DTOs.Payments;
using Maranny.Application.Interfaces;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Services
{
    public class PaymentsManagementService : IPaymentsManagementService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPaymentService _paymentService;

        public PaymentsManagementService(
            ApplicationDbContext dbContext,
            IPaymentService paymentService)
        {
            _dbContext = dbContext;
            _paymentService = paymentService;
        }

        public async Task<(bool success, string message, object? data)> InitiatePaymentAsync(int userId, InitiatePaymentDto dto)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) return (false, "Client profile not found", null);

            var booking = await _dbContext.Bookings
                .Include(b => b.TrainingSession).ThenInclude(s => s.Coach)
                .FirstOrDefaultAsync(b => b.BookingID == dto.BookingID);
            if (booking == null) return (false, "Booking not found", null);
            if (booking.ClientID != client.ClientID) return (false, "Forbidden", null);
            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
                return (false, "Payment cannot be initiated for this booking", null);

            var normalizedMethod = dto.Method?.Trim();
            if (!string.Equals(normalizedMethod, "Card", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalizedMethod, "Wallet", StringComparison.OrdinalIgnoreCase))
                return (false, "Only Card and Wallet payment methods are supported", null);

            var expectedAmount = await _dbContext.CoachSports
                .Where(cs => cs.CoachID == booking.TrainingSession.CoachID &&
                             cs.SportID == booking.TrainingSession.SportID)
                .Select(cs => cs.PricePerSession).FirstOrDefaultAsync();

            if (!expectedAmount.HasValue || expectedAmount.Value <= 0)
                return (false, "Session price is not configured", null);

            if (dto.Amount != expectedAmount.Value)
                return (false, $"Amount mismatch. Expected: {expectedAmount.Value}", null);

            var existingPayment = await _paymentService.GetPaymentByBookingIdAsync(dto.BookingID);
            if (existingPayment != null)
            {
                if (existingPayment.Status == Core.Enums.PaymentStatus.Completed)
                    return (false, "Payment already completed for this booking", null);
                if (existingPayment.Status == Core.Enums.PaymentStatus.Pending)
                    return (false, "Payment already initiated. Please complete existing payment.", null);
            }

            try
            {
                var payment = await _paymentService.InitiatePaymentAsync(
                    dto.BookingID, expectedAmount.Value,
                    NormalizeMethod(normalizedMethod!), client.ClientID);

                var paymentUrl = await _paymentService.GeneratePaymentUrlAsync(payment);

                return (true, "Payment initiated successfully", new
                {
                    paymentId = payment.PaymentID,
                    paymentUrl,
                    amount = payment.Amount,
                    platformFee = payment.PlatformFee,
                    bookingStatus = booking.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                return (false, $"Failed to initiate payment: {ex.Message}", null);
            }
        }

        public async Task<(bool success, string message, object? data)> GetPaymentDetailsAsync(int userId, int paymentId, bool isAdmin)
        {
            var payment = await _dbContext.Payments
                .Include(p => p.Booking)
                .Include(p => p.TrainingSession).ThenInclude(s => s.Coach)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId);

            if (payment == null) return (false, "Payment not found", null);

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);

            bool isOwner = (client != null && payment.ClientID == client.ClientID) ||
                           (coach != null && payment.TrainingSession.CoachID == coach.CoachID);

            if (!isOwner && !isAdmin) return (false, "Forbidden", null);

            return (true, "OK", new
            {
                payment.PaymentID,
                payment.BookingID,
                payment.Amount,
                payment.Method,
                Status = payment.Status.ToString(),
                payment.TransactionDate,
                payment.PlatformFee,
                payment.RefundAmount,
                Session = new
                {
                    payment.TrainingSession.SessionDate,
                    payment.TrainingSession.Start_Time,
                    payment.TrainingSession.End_Time,
                    payment.TrainingSession.Location
                },
                Coach = new
                {
                    Name = payment.TrainingSession.Coach.F_name + " " + payment.TrainingSession.Coach.L_name
                }
            });
        }

        public async Task<(bool success, object? data)> GetMyPaymentsAsync(int userId)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) return (false, null);

            var payments = await _dbContext.Payments
                .Include(p => p.TrainingSession).ThenInclude(s => s.Coach)
                .Where(p => p.ClientID == client.ClientID)
                .OrderByDescending(p => p.TransactionDate)
                .Select(p => new
                {
                    p.PaymentID,
                    p.Amount,
                    p.Method,
                    Status = p.Status.ToString(),
                    p.TransactionDate,
                    Session = new
                    {
                        p.TrainingSession.SessionDate,
                        p.TrainingSession.Start_Time,
                        CoachName = p.TrainingSession.Coach.F_name + " " + p.TrainingSession.Coach.L_name
                    }
                }).ToListAsync();

            return (true, payments);
        }

        private static string NormalizeMethod(string method) =>
            string.Equals(method, "wallet", StringComparison.OrdinalIgnoreCase) ? "Wallet" : "Card";
    }
}