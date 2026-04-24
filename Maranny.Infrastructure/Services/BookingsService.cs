using Maranny.Application.DTOs.Bookings;
using Maranny.Application.DTOs.Sessions;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Maranny.Infrastructure.Services
{
    public class BookingsService : IBookingService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly INotificationService _notificationService;
        private readonly IPaymentService _paymentService;

        public BookingsService(
            ApplicationDbContext dbContext,
            INotificationService notificationService,
            IPaymentService paymentService)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
            _paymentService = paymentService;
        }

        public async Task<(bool success, string message, object? data)> BookSessionAsync(int userId, CreateBookingDto dto)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) return (false, "Client profile not found", null);

            var session = await _dbContext.TrainingSessions
                .Include(s => s.Coach)
                .FirstOrDefaultAsync(s => s.SessionID == dto.SessionID);
            if (session == null) return (false, "Session not found", null);

            if (session.Status != SessionStatus.Scheduled)
                return (false, "Session is not available for booking", null);

            var sessionDateTime = session.SessionDate.Add(session.Start_Time);
            if (sessionDateTime <= DateTime.UtcNow)
                return (false, "Cannot book past sessions", null);

            var currentBookings = await _dbContext.ClientSessions
                .CountAsync(cs => cs.SessionID == dto.SessionID);
            if (currentBookings >= session.MaxParticipants)
                return (false, "Session is fully booked", null);

            var existingBooking = await _dbContext.ClientSessions
                .FirstOrDefaultAsync(cs => cs.ClientID == client.ClientID && cs.SessionID == dto.SessionID);
            if (existingBooking != null)
                return (false, "You have already booked this session", null);

            var sessionPrice = await GetSessionPriceAsync(session.CoachID, session.SportID);
            if (sessionPrice == null)
                return (false, "Session price is not configured for this coach and sport", null);

            var overlapping = await _dbContext.ClientSessions
                .Include(cs => cs.TrainingSession)
                .Where(cs => cs.ClientID == client.ClientID &&
                             cs.TrainingSession.SessionDate.Date == session.SessionDate.Date &&
                             cs.TrainingSession.Status != SessionStatus.Cancelled &&
                             ((session.Start_Time >= cs.TrainingSession.Start_Time && session.Start_Time < cs.TrainingSession.End_Time) ||
                              (session.End_Time > cs.TrainingSession.Start_Time && session.End_Time <= cs.TrainingSession.End_Time) ||
                              (session.Start_Time <= cs.TrainingSession.Start_Time && session.End_Time >= cs.TrainingSession.End_Time)))
                .FirstOrDefaultAsync();
            if (overlapping != null)
                return (false, "You have an overlapping booking at this time", null);

            var booking = new Booking
            {
                SessionID = dto.SessionID,
                ClientID = client.ClientID,
                BookingDate = DateTime.UtcNow,
                Status = BookingStatus.Pending
            };
            _dbContext.Bookings.Add(booking);
            _dbContext.ClientSessions.Add(new ClientSession
            {
                ClientID = client.ClientID,
                SessionID = dto.SessionID
            });
            _dbContext.UserInteractions.Add(new UserInteraction
            {
                UserId = userId,
                CoachId = session.CoachID,
                Type = "Booking",
                Timestamp = DateTime.UtcNow,
                Context = $"Booked session {session.SessionID}"
            });

            await _dbContext.SaveChangesAsync();

            await _notificationService.SendNotificationAsync(
                session.Coach.UserId,
                "New Booking",
                $"You have a new booking for {session.SessionDate:MMM dd} at {session.Start_Time}",
                NotificationType.BookingConfirmation);

            return (true, "Session booked successfully", new
            {
                bookingId = booking.BookingID,
                note = "Please complete payment to confirm your booking",
                totalPrice = sessionPrice,
                bookingStatus = booking.Status.ToString()
            });
        }

        public async Task<(bool success, object? data)> GetMyBookingsAsync(int userId, string? status, string? tab, int page, int pageSize)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) return (false, null);

            var query = _dbContext.Bookings
                .Include(b => b.TrainingSession).ThenInclude(s => s.Coach)
                .Include(b => b.TrainingSession).ThenInclude(s => s.Sport)
                .Where(b => b.ClientID == client.ClientID);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, out var bookingStatus))
                query = query.Where(b => b.Status == bookingStatus);

            if (!string.IsNullOrWhiteSpace(tab))
            {
                var normalizedTab = tab.Trim().ToLowerInvariant();
                var today = DateTime.UtcNow.Date;
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
                .Skip((page - 1) * pageSize).Take(pageSize)
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
                            .Select(cs => cs.PricePerSession).FirstOrDefault()
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
                }).ToListAsync();

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

            return (true, new { totalCount, page, pageSize, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize), bookings = result });
        }

        public async Task<(bool success, string message, object? data)> GetBookingDetailsAsync(int userId, int bookingId)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) return (false, "Client profile not found", null);

            var booking = await _dbContext.Bookings
                .Include(b => b.TrainingSession).ThenInclude(s => s.Coach)
                .Include(b => b.TrainingSession).ThenInclude(s => s.Sport)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null) return (false, "Booking not found", null);
            if (booking.ClientID != client.ClientID) return (false, "Forbidden", null);

            var payment = await _dbContext.Payments
                .Where(p => p.BookingID == booking.BookingID)
                .OrderByDescending(p => p.TransactionDate)
                .Select(p => new { p.PaymentID, p.Amount, p.Method, Status = p.Status.ToString(), p.PlatformFee, p.TransactionDate, p.RefundAmount, p.IsRefunded })
                .FirstOrDefaultAsync();

            var totalPrice = await GetSessionPriceAsync(booking.TrainingSession.CoachID, booking.TrainingSession.SportID);
            var durationMinutes = (int)(booking.TrainingSession.End_Time - booking.TrainingSession.Start_Time).TotalMinutes;

            return (true, "OK", new
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

        public async Task<(bool success, object? data)> GetCoachBookingsAsync(int userId, string? status, string? tab, int page, int pageSize)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null) return (false, null);

            var query = _dbContext.Bookings
                .Include(b => b.Client)
                .Include(b => b.TrainingSession).ThenInclude(s => s.Sport)
                .Where(b => b.TrainingSession.CoachID == coach.CoachID);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var parsedStatus))
                query = query.Where(b => b.Status == parsedStatus);

            if (!string.IsNullOrWhiteSpace(tab))
            {
                var normalizedTab = tab.Trim().ToLowerInvariant();
                var today = DateTime.UtcNow.Date;
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
                .Skip((page - 1) * pageSize).Take(pageSize)
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
                }).ToListAsync();

            return (true, new { totalCount, page, pageSize, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize), bookings });
        }

        public async Task<(bool success, string message)> ApproveBookingAsync(int userId, int bookingId)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null) return (false, "Coach profile not found");

            var booking = await _dbContext.Bookings.Include(b => b.TrainingSession)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);
            if (booking == null) return (false, "Booking not found");
            if (booking.TrainingSession.CoachID != coach.CoachID) return (false, "Forbidden");
            if (booking.Status != BookingStatus.Pending) return (false, "Only pending bookings can be approved");

            booking.Status = BookingStatus.Confirmed;
            await _dbContext.SaveChangesAsync();

            var clientUserId = await _dbContext.Clients
                .Where(c => c.ClientID == booking.ClientID).Select(c => c.UserId).FirstOrDefaultAsync();
            if (clientUserId != 0)
                await _notificationService.SendNotificationAsync(clientUserId, "Booking Confirmed",
                    $"Your booking for {booking.TrainingSession.SessionDate:MMM dd} has been approved.",
                    NotificationType.BookingConfirmation);

            return (true, "Booking approved successfully");
        }

        public async Task<(bool success, string message)> DeclineBookingAsync(int userId, int bookingId, CoachBookingActionDto? dto)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null) return (false, "Coach profile not found");

            var booking = await _dbContext.Bookings.Include(b => b.TrainingSession)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);
            if (booking == null) return (false, "Booking not found");
            if (booking.TrainingSession.CoachID != coach.CoachID) return (false, "Forbidden");
            if (booking.Status != BookingStatus.Pending) return (false, "Only pending bookings can be declined");

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.CancelledByCoach = true;
            booking.CancellationReason = dto?.Reason ?? "Declined by coach";
            await _dbContext.SaveChangesAsync();

            var clientUserId = await _dbContext.Clients
                .Where(c => c.ClientID == booking.ClientID).Select(c => c.UserId).FirstOrDefaultAsync();
            if (clientUserId != 0)
                await _notificationService.SendNotificationAsync(clientUserId, "Booking Declined",
                    $"Your booking for {booking.TrainingSession.SessionDate:MMM dd} was declined.",
                    NotificationType.BookingCancellation);

            return (true, "Booking declined successfully");
        }

        public async Task<(bool success, string message, object? data)> CancelBookingAsync(int userId, int bookingId, string? reason)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) return (false, "Client profile not found", null);

            var booking = await _dbContext.Bookings.Include(b => b.TrainingSession)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);
            if (booking == null) return (false, "Booking not found", null);
            if (booking.ClientID != client.ClientID) return (false, "Forbidden", null);
            if (booking.Status == BookingStatus.Cancelled) return (false, "Booking is already cancelled", null);
            if (booking.Status == BookingStatus.Completed) return (false, "Cannot cancel completed booking", null);

            var sessionStart = booking.TrainingSession.SessionDate.Add(booking.TrainingSession.Start_Time);
            if (sessionStart <= DateTime.UtcNow) return (false, "Cannot cancel a session that has already started", null);

            var hoursUntil = (sessionStart - DateTime.UtcNow).TotalHours;
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.CancellationReason = reason ?? "Cancelled by client";
            booking.CancelledByCoach = false;

            string refundMessage = "";
            var payment = await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.BookingID == bookingId && p.Status == PaymentStatus.Completed);

            if (payment != null)
            {
                if (hoursUntil >= 24)
                {
                    var refundAmount = payment.Amount * 0.90m;
                    await _paymentService.ProcessRefundAsync(payment.PaymentID, refundAmount,
                        $"Cancelled {hoursUntil:F1} hours before session. 90% refund.");
                    refundMessage = $"Refund of {refundAmount:F2} EGP will be processed (90%).";
                }
                else
                {
                    payment.RefundReason = $"Cancelled {hoursUntil:F1} hours before. No refund.";
                    refundMessage = "No refund. Cancellation within 24 hours.";
                }
            }

            await _dbContext.SaveChangesAsync();

            var coachUserId = await _dbContext.Coaches
                .Where(c => c.CoachID == booking.TrainingSession.CoachID).Select(c => c.UserId).FirstOrDefaultAsync();
            if (coachUserId != 0)
                await _notificationService.SendNotificationAsync(coachUserId, "Booking Cancelled",
                    $"A booking for {booking.TrainingSession.SessionDate:MMM dd} was cancelled by the client.",
                    NotificationType.BookingCancellation);

            return (true, "Booking cancelled successfully", new { refundInfo = refundMessage, hoursUntilSession = hoursUntil });
        }

        public async Task<(bool success, string message, object? data)> CoachCancelSessionAsync(int userId, int sessionId, string? reason)
        {
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null) return (false, "Coach profile not found", null);

            var session = await _dbContext.TrainingSessions.FirstOrDefaultAsync(s => s.SessionID == sessionId);
            if (session == null) return (false, "Session not found", null);
            if (session.CoachID != coach.CoachID) return (false, "Forbidden", null);
            if (session.Status == SessionStatus.Cancelled) return (false, "Session is already cancelled", null);
            if (session.Status == SessionStatus.Completed) return (false, "Cannot cancel completed session", null);

            var sessionStart = session.SessionDate.Add(session.Start_Time);
            if (sessionStart <= DateTime.UtcNow) return (false, "Cannot cancel a session that has already started", null);

            var bookings = await _dbContext.Bookings
                .Where(b => b.SessionID == sessionId &&
                            (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                .ToListAsync();

            int refundedCount = 0;
            decimal totalRefunded = 0;

            foreach (var booking in bookings)
            {
                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = DateTime.UtcNow;
                booking.CancelledByCoach = true;
                booking.CancellationReason = reason ?? "Cancelled by coach";

                var payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.BookingID == booking.BookingID && p.Status == PaymentStatus.Completed);

                if (payment != null)
                {
                    await _paymentService.ProcessRefundAsync(payment.PaymentID, payment.Amount, "Coach cancelled. Full refund.");
                    refundedCount++;
                    totalRefunded += payment.Amount;

                    var clientUserId = await _dbContext.Clients
                        .Where(c => c.ClientID == booking.ClientID).Select(c => c.UserId).FirstOrDefaultAsync();
                    if (clientUserId != 0)
                        await _notificationService.SendNotificationAsync(clientUserId, "Session Cancelled by Coach",
                            $"Your session on {session.SessionDate:MMM dd} was cancelled. Full refund of {payment.Amount:F2} EGP.",
                            NotificationType.BookingCancellation);
                }
            }

            session.Status = SessionStatus.Cancelled;
            await _dbContext.SaveChangesAsync();

            return (true, "Session cancelled successfully", new
            {
                bookingsCancelled = bookings.Count,
                refundsIssued = refundedCount,
                totalRefundAmount = totalRefunded
            });
        }

        private async Task<decimal?> GetSessionPriceAsync(int coachId, int sportId)
        {
            return await _dbContext.CoachSports
                .Where(cs => cs.CoachID == coachId && cs.SportID == sportId)
                .Select(cs => cs.PricePerSession).FirstOrDefaultAsync();
        }
    }
}