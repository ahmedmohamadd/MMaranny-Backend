using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Application.Interfaces;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Maranny.Infrastructure.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public PaymentService(
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<Payment> InitiatePaymentAsync(int bookingId, decimal amount, string method, int clientId)
        {
            // Get booking
            var booking = await _dbContext.Bookings
                .Include(b => b.TrainingSession)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
            {
                throw new Exception("Booking not found");
            }

            // Create payment record
            var payment = new Payment
            {
                BookingID = bookingId,
                SessionID = booking.SessionID,
                ClientID = clientId,
                Amount = amount,
                Method = method,
                Status = PaymentStatus.Pending,
                TransactionDate = DateTime.UtcNow,
                PlatformFee = amount * 0.10m, // 10% platform fee
                PaymentGateway = "Paymob"
            };

            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync();

            return payment;
        }

        public async Task<string> GeneratePaymentUrlAsync(Payment payment)
        {
            // TODO: Integrate with Paymob API
            // For now, return a placeholder URL
            // In production, this would call Paymob's API to get payment iframe URL

            var paymobApiKey = _configuration["PaymobSettings:ApiKey"];
            var paymobIntegrationId = _configuration["PaymobSettings:IntegrationId"];

            if (string.IsNullOrEmpty(paymobApiKey))
            {
                // Return mock URL for testing
                return $"https://payment-gateway.example.com/pay/{payment.PaymentID}";
            }

            // Real Paymob integration would go here:
            // 1. Get auth token
            // 2. Create order
            // 3. Generate payment key
            // 4. Return iframe URL

            return $"https://accept.paymob.com/api/acceptance/iframes/[IFRAME_ID]?payment_token=[TOKEN]";
        }

        public async Task<bool> VerifyPaymentAsync(string transactionId)
        {
            // TODO: Verify payment with Paymob API
            // For now, return true (mock)
            // In production, call Paymob's API to verify transaction

            return await Task.FromResult(true);
        }

        public async Task UpdatePaymentStatusAsync(int paymentId, string status)
        {
            var payment = await _dbContext.Payments.FindAsync(paymentId);
            if (payment == null)
            {
                throw new Exception("Payment not found");
            }

            payment.Status = Enum.Parse<PaymentStatus>(status, true);

            // If payment completed, update booking status
            if (payment.Status == PaymentStatus.Completed)
            {
                var booking = await _dbContext.Bookings.FindAsync(payment.BookingID);
                if (booking != null)
                {
                    booking.Status = BookingStatus.Confirmed;
                }
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task<Payment?> GetPaymentByBookingIdAsync(int bookingId)
        {
            return await _dbContext.Payments
                .AsNoTracking()
                .Include(p => p.Booking)
                .Include(p => p.TrainingSession)
                .FirstOrDefaultAsync(p => p.BookingID == bookingId);
        }

        public async Task<bool> ProcessRefundAsync(int paymentId, decimal refundAmount, string reason)
        {
            var payment = await _dbContext.Payments.FindAsync(paymentId);

            if (payment == null)
            {
                throw new Exception("Payment not found");
            }

            if (payment.Status != PaymentStatus.Completed)
            {
                throw new Exception("Cannot refund a payment that is not completed");
            }

            if (payment.IsRefunded)
            {
                throw new Exception("Payment already refunded");
            }

            // TODO: In production, call Paymob refund API here
            // For now, we just update the database

            payment.RefundAmount = refundAmount;
            payment.RefundedAt = DateTime.UtcNow;
            payment.RefundReason = reason;
            payment.IsRefunded = true;
            payment.Status = PaymentStatus.Refunded;

            await _dbContext.SaveChangesAsync();

            return true;
        }
    }
}
