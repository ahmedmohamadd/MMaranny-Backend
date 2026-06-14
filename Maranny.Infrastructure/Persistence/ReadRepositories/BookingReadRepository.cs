using Maranny.Application.Abstractions.Common;
using Maranny.Application.Abstractions.Persistence;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.ReadRepositories
{
    public sealed class BookingReadRepository : IBookingReadRepository
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IClock _clock;

        public BookingReadRepository(ApplicationDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext;
            _clock = clock;
        }

        public async Task<object?> GetClientBookingsAsync(
            int userId,
            string? status,
            string? tab,
            int page,
            int pageSize)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return null;
            }

            var query = _dbContext.Bookings
                .Include(b => b.TrainingSession).ThenInclude(s => s.Coach)
                .Include(b => b.TrainingSession).ThenInclude(s => s.Sport)
                .Where(b => b.ClientID == client.ClientID);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, out var bookingStatus))
            {
                query = query.Where(b => b.Status == bookingStatus);
            }

            if (!string.IsNullOrWhiteSpace(tab))
            {
                var normalizedTab = tab.Trim().ToLowerInvariant();
                var today = _clock.UtcNow.Date;
                query = normalizedTab switch
                {
                    "upcoming" => query.Where(b => b.TrainingSession.SessionDate >= today &&
                        (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)),
                    "pending" or "pendingrequests" => query.Where(b => b.Status == BookingStatus.Pending),
                    "past" => query.Where(b => b.TrainingSession.SessionDate < today ||
                        b.Status == BookingStatus.Completed || b.Status == BookingStatus.Cancelled),
                    _ => query
                };
            }

            var totalCount = await query.CountAsync();
            var bookings = await query
                .OrderByDescending(b => b.BookingDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.BookingID,
                    b.BookingDate,
                    b.CancelledAt,
                    b.CancellationReason,
                    b.CancelledByCoach,
                    Status = b.Status.ToString(),
                    Session = new
                    {
                        b.TrainingSession.SessionID,
                        b.TrainingSession.SessionDate,
                        b.TrainingSession.SessionType,
                        b.TrainingSession.Location,
                        b.TrainingSession.Start_Time,
                        b.TrainingSession.End_Time,
                        SportName = b.TrainingSession.Sport.Name,
                        Price = _dbContext.CoachSports
                            .Where(cs => cs.CoachID == b.TrainingSession.CoachID && cs.SportID == b.TrainingSession.SportID)
                            .Select(cs => cs.PricePerSession)
                            .FirstOrDefault()
                    },
                    Coach = new
                    {
                        b.TrainingSession.Coach.CoachID,
                        Name = b.TrainingSession.Coach.F_name + " " + b.TrainingSession.Coach.L_name,
                        b.TrainingSession.Coach.AvgRating
                    },
                    Payment = _dbContext.Payments
                        .Where(p => p.BookingID == b.BookingID)
                        .Select(p => new { p.PaymentID, p.Amount, p.Method, Status = p.Status.ToString() })
                        .FirstOrDefault()
                })
                .ToListAsync();

            var result = bookings.Select(b => new
            {
                b.BookingID,
                b.BookingDate,
                b.CancelledAt,
                b.CancellationReason,
                b.CancelledByCoach,
                b.Status,
                b.Session,
                b.Coach,
                b.Payment,
                canCancel = b.Status == BookingStatus.Pending.ToString() || b.Status == BookingStatus.Confirmed.ToString(),
                canPay = b.Status == BookingStatus.Pending.ToString() &&
                         (b.Payment == null || b.Payment.Status != PaymentStatus.Completed.ToString()),
                canReview = b.Status == BookingStatus.Completed.ToString()
            });

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                bookings = result
            };
        }

        public async Task<(string? error, object? data)> GetClientBookingDetailsAsync(int userId, int bookingId)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return ("Client profile not found", null);
            }

            var booking = await _dbContext.Bookings
                .Include(b => b.TrainingSession).ThenInclude(s => s.Coach)
                .Include(b => b.TrainingSession).ThenInclude(s => s.Sport)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
            {
                return ("Booking not found", null);
            }

            if (booking.ClientID != client.ClientID)
            {
                return ("Forbidden", null);
            }

            var payment = await _dbContext.Payments
                .Where(p => p.BookingID == booking.BookingID)
                .OrderByDescending(p => p.TransactionDate)
                .Select(p => new
                {
                    p.PaymentID,
                    p.Amount,
                    p.Method,
                    Status = p.Status.ToString(),
                    p.PlatformFee,
                    p.TransactionDate,
                    p.RefundAmount,
                    p.IsRefunded
                })
                .FirstOrDefaultAsync();

            var totalPrice = await _dbContext.CoachSports
                .Where(cs => cs.CoachID == booking.TrainingSession.CoachID && cs.SportID == booking.TrainingSession.SportID)
                .Select(cs => cs.PricePerSession)
                .FirstOrDefaultAsync();

            var durationMinutes = (int)(booking.TrainingSession.End_Time - booking.TrainingSession.Start_Time).TotalMinutes;

            return (null, new
            {
                booking.BookingID,
                booking.BookingDate,
                status = booking.Status.ToString(),
                booking.CancelledAt,
                booking.CancellationReason,
                booking.CancelledByCoach,
                session = new
                {
                    booking.TrainingSession.SessionID,
                    booking.TrainingSession.SessionDate,
                    booking.TrainingSession.Start_Time,
                    booking.TrainingSession.End_Time,
                    durationMinutes,
                    booking.TrainingSession.SessionType,
                    booking.TrainingSession.Location,
                    sportName = booking.TrainingSession.Sport.Name,
                    totalPrice
                },
                coach = new
                {
                    booking.TrainingSession.Coach.CoachID,
                    name = booking.TrainingSession.Coach.F_name + " " + booking.TrainingSession.Coach.L_name,
                    booking.TrainingSession.Coach.AvgRating,
                    verificationStatus = booking.TrainingSession.Coach.VerificationStatus.ToString()
                },
                payment,
                canCancel = booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed,
                canPay = booking.Status == BookingStatus.Pending && (payment == null || payment.Status != PaymentStatus.Completed.ToString()),
                canReview = booking.Status == BookingStatus.Completed
            });
        }

        public async Task<object?> GetCoachBookingsAsync(
            int userId,
            string? status,
            string? tab,
            int page,
            int pageSize)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return null;
            }

            var query = _dbContext.Bookings
                .Include(b => b.Client)
                .Include(b => b.TrainingSession).ThenInclude(s => s.Sport)
                .Where(b => b.TrainingSession.CoachID == coach.CoachID);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(b => b.Status == parsedStatus);
            }

            if (!string.IsNullOrWhiteSpace(tab))
            {
                var normalizedTab = tab.Trim().ToLowerInvariant();
                var today = _clock.UtcNow.Date;
                query = normalizedTab switch
                {
                    "today" => query.Where(b => b.TrainingSession.SessionDate.Date == today),
                    "pending" or "pendingrequests" => query.Where(b => b.Status == BookingStatus.Pending),
                    "recent" or "recentreviews" => query.Where(b => b.Status == BookingStatus.Completed || b.Status == BookingStatus.Confirmed),
                    _ => query
                };
            }

            var totalCount = await query.CountAsync();
            var bookings = await query
                .OrderByDescending(b => b.BookingDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.BookingID,
                    b.BookingDate,
                    status = b.Status.ToString(),
                    b.CancelledAt,
                    b.CancellationReason,
                    session = new
                    {
                        b.TrainingSession.SessionID,
                        b.TrainingSession.SessionDate,
                        b.TrainingSession.Start_Time,
                        b.TrainingSession.End_Time,
                        b.TrainingSession.Location,
                        b.TrainingSession.SessionType,
                        sportName = b.TrainingSession.Sport.Name
                    },
                    client = new { b.Client.ClientID, name = b.Client.F_name + " " + b.Client.L_name, b.Client.URL },
                    canAccept = b.Status == BookingStatus.Pending,
                    canDecline = b.Status == BookingStatus.Pending
                })
                .ToListAsync();

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                bookings
            };
        }
    }
}
