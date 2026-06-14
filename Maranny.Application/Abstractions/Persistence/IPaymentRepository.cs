using Maranny.Core.Entities;

namespace Maranny.Application.Abstractions.Persistence
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByIdWithDetailsAsync(int paymentId);
        Task<Payment?> GetByBookingIdAsync(int bookingId);
        Task<Payment?> GetCompletedByBookingIdAsync(int bookingId);
    }
}
