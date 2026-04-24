using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Maranny.Core.Entities;

using Maranny.Core.Entities;

namespace Maranny.Application.Interfaces
{
    public interface IPaymentService
    {
        Task<Payment> InitiatePaymentAsync(int bookingId, decimal amount, string method, int clientId);
        Task<string> GeneratePaymentUrlAsync(Payment payment);
        Task<bool> VerifyPaymentAsync(string transactionId);
        Task UpdatePaymentStatusAsync(int paymentId, string status);
        Task<Payment?> GetPaymentByBookingIdAsync(int bookingId);
        Task<bool> ProcessRefundAsync(int paymentId, decimal refundAmount, string reason);
    }
}