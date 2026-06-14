using Maranny.Application.Abstractions.Persistence;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.ReadRepositories
{
    public sealed class PaymentReadRepository : IPaymentReadRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public PaymentReadRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(string? error, object? data)> GetPaymentDetailsAsync(int userId, int paymentId, bool isAdmin)
        {
            var payment = await _dbContext.Payments
                .Include(p => p.Booking)
                .Include(p => p.TrainingSession).ThenInclude(s => s.Coach)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId);

            if (payment == null)
            {
                return ("Payment not found", null);
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);

            var isOwner = (client != null && payment.ClientID == client.ClientID) ||
                          (coach != null && payment.TrainingSession.CoachID == coach.CoachID);

            if (!isOwner && !isAdmin)
            {
                return ("Forbidden", null);
            }

            return (null, new
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

        public async Task<object?> GetClientPaymentsAsync(int userId)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return null;
            }

            return await _dbContext.Payments
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
                })
                .ToListAsync();
        }
    }
}
