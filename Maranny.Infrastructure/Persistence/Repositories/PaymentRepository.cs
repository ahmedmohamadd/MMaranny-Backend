using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.Repositories
{
    public sealed class PaymentRepository : IPaymentRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public PaymentRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<Payment?> GetByIdWithDetailsAsync(int paymentId)
        {
            return _dbContext.Payments
                .Include(p => p.Booking)
                .Include(p => p.TrainingSession).ThenInclude(s => s.Coach)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId);
        }

        public Task<Payment?> GetByBookingIdAsync(int bookingId)
        {
            return _dbContext.Payments
                .AsNoTracking()
                .Include(p => p.Booking)
                .Include(p => p.TrainingSession)
                .FirstOrDefaultAsync(p => p.BookingID == bookingId);
        }

        public Task<Payment?> GetCompletedByBookingIdAsync(int bookingId)
        {
            return _dbContext.Payments
                .FirstOrDefaultAsync(p => p.BookingID == bookingId && p.Status == PaymentStatus.Completed);
        }
    }
}
