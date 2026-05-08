using Maranny.Application.DTOs.Sessions;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Core.Interfaces;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/bookings")]
    [Authorize]
    public class BookingsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly INotificationService _notificationService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(
            ApplicationDbContext dbContext,
            INotificationService notificationService,
            IPaymentService paymentService,
            ILogger<BookingsController> logger)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> BookSession(CreateBookingDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return NotFound(new { error = "Client profile not found" });
            }

            var resolution = await ResolveBookingSessionAsync(dto);
            if (resolution.ErrorResult != null)
            {
                return resolution.ErrorResult;
            }

            var session = resolution.Session!;

            if (session.Status != SessionStatus.Scheduled)
            {
                return BadRequest(new { error = "Session is not available for booking" });
            }

            var sessionDurationMinutes = (session.End_Time - session.Start_Time).TotalMinutes;
            if (sessionDurationMinutes < 45 || sessionDurationMinutes > 60)
            {
                return BadRequest(new { error = "Session duration must be between 45 and 60 minutes" });
            }

            var sessionDateTime = session.SessionDate.Add(session.Start_Time);
            if (sessionDateTime <= DateTime.UtcNow)
            {
                return BadRequest(new { error = "Cannot book past sessions" });
            }

            var currentBookings = await GetActiveBookingCountAsync(session.SessionID);
            if (session.MaxParticipants.HasValue && currentBookings >= session.MaxParticipants.Value)
            {
                return BadRequest(new { error = "Session is fully booked" });
            }

            var existingBooking = await _dbContext.Bookings
                .FirstOrDefaultAsync(b => b.ClientID == client.ClientID &&
                                          b.SessionID == session.SessionID &&
                                          b.Status != BookingStatus.Cancelled);

            if (existingBooking != null)
            {
                return BadRequest(new { error = "You have already booked this session" });
            }

            var sessionPrice = await GetSessionPriceAsync(session.CoachID, session.SportID);
            if (sessionPrice == null || sessionPrice.Value <= 0)
            {
                return BadRequest(new { error = "Session price is not configured for this coach and sport" });
            }

            var overlappingBooking = await _dbContext.Bookings
                .Include(b => b.TrainingSession)
                .Where(b => b.ClientID == client.ClientID &&
                            (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                            b.TrainingSession.SessionDate.Date == session.SessionDate.Date &&
                            b.TrainingSession.Status != SessionStatus.Cancelled &&
                            ((session.Start_Time >= b.TrainingSession.Start_Time && session.Start_Time < b.TrainingSession.End_Time) ||
                             (session.End_Time > b.TrainingSession.Start_Time && session.End_Time <= b.TrainingSession.End_Time) ||
                             (session.Start_Time <= b.TrainingSession.Start_Time && session.End_Time >= b.TrainingSession.End_Time)))
                .FirstOrDefaultAsync();

            if (overlappingBooking != null)
            {
                return BadRequest(new { error = "You have an overlapping booking at this time" });
            }

            var activeBookingsOnSameDay = await _dbContext.Bookings
                .Include(b => b.TrainingSession)
                .CountAsync(b => b.ClientID == client.ClientID &&
                                 (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                                 b.TrainingSession.SessionDate.Date == session.SessionDate.Date &&
                                 b.TrainingSession.Status != SessionStatus.Cancelled);

            if (activeBookingsOnSameDay >= 2)
            {
                return BadRequest(new { error = "You can only book up to 2 sessions per day." });
            }

            var booking = new Booking
            {
                SessionID = session.SessionID,
                ClientID = client.ClientID,
                BookingDate = DateTime.UtcNow,
                Status = BookingStatus.Pending
            };
            _dbContext.Bookings.Add(booking);

            var hasClientSessionLink = await _dbContext.ClientSessions
                .AnyAsync(cs => cs.ClientID == client.ClientID && cs.SessionID == session.SessionID);

            if (!hasClientSessionLink)
            {
                _dbContext.ClientSessions.Add(new ClientSession
                {
                    ClientID = client.ClientID,
                    SessionID = session.SessionID
                });
            }

            _dbContext.UserInteractions.Add(new UserInteraction
            {
                UserId = userId,
                CoachId = session.CoachID,
                Type = "Booking",
                Timestamp = DateTime.UtcNow,
                Context = $"Booked session {session.SessionID}"
            });

            await _dbContext.SaveChangesAsync();

            await TrySendNotificationAsync(
                session.Coach.UserId,
                "New Booking",
                $"You have a new booking for {session.SessionDate:MMM dd} at {session.Start_Time}",
                NotificationType.BookingConfirmation);

            return Ok(new
            {
                message = "Session booked successfully",
                bookingId = booking.BookingID,
                sessionId = session.SessionID,
                note = "Proceed to payment method selection",
                totalPrice = sessionPrice,
                bookingStatus = booking.Status.ToString(),
                autoCreatedSession = resolution.AutoCreatedSession
            });
        }

        [HttpGet("my")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetMyBookings(
            [FromQuery] string? status = null,
            [FromQuery] string? tab = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return NotFound(new { error = "Client profile not found" });
            }

            var query = _dbContext.Bookings
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Coach)
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Sport)
                .Where(b => b.ClientID == client.ClientID);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, out var bookingStatus))
            {
                query = query.Where(b => b.Status == bookingStatus);
            }

            if (!string.IsNullOrWhiteSpace(tab))
            {
                var normalizedTab = tab.Trim().ToLowerInvariant();
                var today = DateTime.UtcNow.Date;

                query = normalizedTab switch
                {
                    "upcoming" => query.Where(b =>
                        b.TrainingSession.SessionDate >= today &&
                        (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)),
                    "pending" or "pendingrequests" => query.Where(b => b.Status == BookingStatus.Pending),
                    "past" => query.Where(b =>
                        b.TrainingSession.SessionDate < today ||
                        b.Status == BookingStatus.Completed ||
                        b.Status == BookingStatus.Cancelled),
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
                        UserID = b.TrainingSession.Coach.UserId,
                        Name = b.TrainingSession.Coach.F_name + " " + b.TrainingSession.Coach.L_name,
                        b.TrainingSession.Coach.AvgRating
                    },
                    Payment = _dbContext.Payments
                        .Where(p => p.BookingID == b.BookingID)
                        .Select(p => new
                        {
                            p.PaymentID,
                            p.Amount,
                            p.Method,
                            Status = p.Status.ToString()
                        })
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
                canPay = b.Status == BookingStatus.Pending.ToString() && b.Payment == null,
                canReview = b.Status == BookingStatus.Completed.ToString()
            });

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                bookings = result
            });
        }

        [HttpGet("{bookingId:int}")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetBookingDetails(int bookingId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return NotFound(new { error = "Client profile not found" });
            }

            var booking = await _dbContext.Bookings
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Coach)
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Sport)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
            {
                return NotFound(new { error = "Booking not found" });
            }

            if (booking.ClientID != client.ClientID)
            {
                return Forbid();
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

            var totalPrice = await GetSessionPriceAsync(booking.TrainingSession.CoachID, booking.TrainingSession.SportID);
            var durationMinutes = (int)(booking.TrainingSession.End_Time - booking.TrainingSession.Start_Time).TotalMinutes;

            return Ok(new
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
                    userId = booking.TrainingSession.Coach.UserId,
                    name = booking.TrainingSession.Coach.F_name + " " + booking.TrainingSession.Coach.L_name,
                    booking.TrainingSession.Coach.AvgRating,
                    verificationStatus = booking.TrainingSession.Coach.VerificationStatus.ToString()
                },
                payment,
                canCancel = booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed,
                canPay = booking.Status == BookingStatus.Pending && payment == null,
                canReview = booking.Status == BookingStatus.Completed
            });
        }

        [HttpGet("coach/my")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> GetCoachBookings(
            [FromQuery] string? status = null,
            [FromQuery] string? tab = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            var query = _dbContext.Bookings
                .Include(b => b.Client)
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Sport)
                .Where(b => b.TrainingSession.CoachID == coach.CoachID);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(b => b.Status == parsedStatus);
            }

            if (!string.IsNullOrWhiteSpace(tab))
            {
                var normalizedTab = tab.Trim().ToLowerInvariant();
                var today = DateTime.UtcNow.Date;

                query = normalizedTab switch
                {
                    "today" => query.Where(b => b.TrainingSession.SessionDate.Date == today),
                    "pending" or "pendingrequests" => query.Where(b => b.Status == BookingStatus.Pending),
                    "recent" or "recentreviews" => query.Where(b =>
                        b.Status == BookingStatus.Completed || b.Status == BookingStatus.Confirmed),
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
                    client = new
                    {
                        b.Client.ClientID,
                        name = b.Client.F_name + " " + b.Client.L_name,
                        b.Client.URL
                    },
                    canAccept = b.Status == BookingStatus.Pending,
                    canDecline = b.Status == BookingStatus.Pending
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                bookings
            });
        }

        [HttpPut("{bookingId}/approve")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> ApproveBooking(int bookingId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            var booking = await _dbContext.Bookings
                .Include(b => b.TrainingSession)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
            {
                return NotFound(new { error = "Booking not found" });
            }

            if (booking.TrainingSession.CoachID != coach.CoachID)
            {
                return Forbid();
            }

            if (booking.Status != BookingStatus.Pending)
            {
                return BadRequest(new { error = "Only pending bookings can be approved" });
            }

            booking.Status = BookingStatus.Confirmed;
            await _dbContext.SaveChangesAsync();

            var clientUserId = await _dbContext.Clients
                .Where(c => c.ClientID == booking.ClientID)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();

            if (clientUserId != 0)
            {
                await TrySendNotificationAsync(
                    clientUserId,
                    "Booking Confirmed",
                    $"Your booking for {booking.TrainingSession.SessionDate:MMM dd} has been approved by the coach.",
                    NotificationType.BookingConfirmation);
            }

            return Ok(new { message = "Booking approved successfully" });
        }

        [HttpPut("{bookingId}/decline")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> DeclineBooking(int bookingId, [FromBody] Maranny.Application.DTOs.Bookings.CoachBookingActionDto? dto = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            var booking = await _dbContext.Bookings
                .Include(b => b.TrainingSession)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
            {
                return NotFound(new { error = "Booking not found" });
            }

            if (booking.TrainingSession.CoachID != coach.CoachID)
            {
                return Forbid();
            }

            if (booking.Status != BookingStatus.Pending)
            {
                return BadRequest(new { error = "Only pending bookings can be declined" });
            }

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.CancelledByCoach = true;
            booking.CancellationReason = dto?.Reason ?? "Declined by coach";

            await RemoveClientSessionLinkAsync(booking.ClientID, booking.SessionID);
            await MarkPendingPaymentAsFailedAsync(booking.BookingID, "Booking declined by coach before payment completion.");
            await _dbContext.SaveChangesAsync();

            var clientUserId = await _dbContext.Clients
                .Where(c => c.ClientID == booking.ClientID)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();

            if (clientUserId != 0)
            {
                await TrySendNotificationAsync(
                    clientUserId,
                    "Booking Declined",
                    $"Your booking for {booking.TrainingSession.SessionDate:MMM dd} was declined by the coach.",
                    NotificationType.BookingCancellation);
            }

            return Ok(new { message = "Booking declined successfully" });
        }

        [HttpPut("{bookingId}/cancel")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> CancelBooking(int bookingId, [FromQuery] string? reason = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return NotFound(new { error = "Client profile not found" });
            }

            var booking = await _dbContext.Bookings
                .Include(b => b.TrainingSession)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
            {
                return NotFound(new { error = "Booking not found" });
            }

            if (booking.ClientID != client.ClientID)
            {
                return Forbid();
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                return BadRequest(new { error = "Booking is already cancelled" });
            }

            if (booking.Status == BookingStatus.Completed)
            {
                return BadRequest(new { error = "Cannot cancel completed booking" });
            }

            var sessionStartDateTime = booking.TrainingSession.SessionDate.Add(booking.TrainingSession.Start_Time);
            if (sessionStartDateTime <= DateTime.UtcNow)
            {
                return BadRequest(new { error = "Cannot cancel booking for sessions that have already started" });
            }

            var hoursUntilSession = (sessionStartDateTime - DateTime.UtcNow).TotalHours;

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.CancellationReason = reason ?? "Cancelled by client";
            booking.CancelledByCoach = false;

            var payment = await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.BookingID == bookingId && p.Status == PaymentStatus.Completed);

            string refundMessage = string.Empty;

            if (payment != null)
            {
                decimal refundAmount;
                string refundReason;

                if (hoursUntilSession >= 24)
                {
                    refundAmount = payment.Amount * 0.90m;
                    refundReason = $"Cancelled {hoursUntilSession:F1} hours before session. 90% refund issued (10% service fee retained).";
                    refundMessage = $"Refund of {refundAmount:F2} EGP will be processed (90% of payment). 10% service fee retained.";
                }
                else
                {
                    refundAmount = 0;
                    refundReason = $"Cancelled only {hoursUntilSession:F1} hours before session. No refund as per cancellation policy.";
                    refundMessage = "No refund issued. Cancellation was within 24 hours of session start.";
                }

                if (refundAmount > 0)
                {
                    await _paymentService.ProcessRefundAsync(payment.PaymentID, refundAmount, refundReason);
                }
                else
                {
                    payment.RefundReason = refundReason;
                }
            }
            else
            {
                await MarkPendingPaymentAsFailedAsync(booking.BookingID, "Booking cancelled by client before payment completion.");
            }

            await RemoveClientSessionLinkAsync(booking.ClientID, booking.SessionID);
            await _dbContext.SaveChangesAsync();

            var coachUserId = await _dbContext.Coaches
                .Where(c => c.CoachID == booking.TrainingSession.CoachID)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();

            if (coachUserId != 0)
            {
                await TrySendNotificationAsync(
                    coachUserId,
                    "Booking Cancelled",
                    $"A booking for {booking.TrainingSession.SessionDate:MMM dd} has been cancelled by the client",
                    NotificationType.BookingCancellation);
            }

            return Ok(new
            {
                message = "Booking cancelled successfully",
                refundInfo = refundMessage,
                hoursUntilSession
            });
        }

        [HttpPut("session/{sessionId}/cancel-by-coach")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CoachCancelSession(int sessionId, [FromQuery] string? reason = null)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            var session = await _dbContext.TrainingSessions.FirstOrDefaultAsync(s => s.SessionID == sessionId);
            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }

            if (session.CoachID != coach.CoachID)
            {
                return Forbid();
            }

            if (session.Status == SessionStatus.Cancelled)
            {
                return BadRequest(new { error = "Session is already cancelled" });
            }

            if (session.Status == SessionStatus.Completed)
            {
                return BadRequest(new { error = "Cannot cancel completed session" });
            }

            var sessionStartDateTime = session.SessionDate.Add(session.Start_Time);
            if (sessionStartDateTime <= DateTime.UtcNow)
            {
                return BadRequest(new { error = "Cannot cancel session that has already started" });
            }

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
                booking.CancellationReason = reason ?? "Cancelled by coach";
                booking.CancelledByCoach = true;

                var payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.BookingID == booking.BookingID && p.Status == PaymentStatus.Completed);

                if (payment != null)
                {
                    var fullRefund = payment.Amount;
                    await _paymentService.ProcessRefundAsync(
                        payment.PaymentID,
                        fullRefund,
                        "Coach cancelled session. Full refund issued.");

                    refundedCount++;
                    totalRefunded += fullRefund;
                }
                else
                {
                    await MarkPendingPaymentAsFailedAsync(booking.BookingID, "Session cancelled by coach before payment completion.");
                }

                await RemoveClientSessionLinkAsync(booking.ClientID, booking.SessionID);

                var clientUserId = await _dbContext.Clients
                    .Where(c => c.ClientID == booking.ClientID)
                    .Select(c => c.UserId)
                    .FirstOrDefaultAsync();

                if (clientUserId != 0)
                {
                    var cancellationMessage = payment != null
                        ? $"Your session on {session.SessionDate:MMM dd} has been cancelled. Full refund of {payment.Amount:F2} EGP will be processed."
                        : $"Your session on {session.SessionDate:MMM dd} has been cancelled by the coach.";

                    await TrySendNotificationAsync(
                        clientUserId,
                        "Session Cancelled by Coach",
                        cancellationMessage,
                        NotificationType.BookingCancellation);
                }
            }

            session.Status = SessionStatus.Cancelled;
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Session cancelled successfully",
                bookingsCancelled = bookings.Count,
                refundsIssued = refundedCount,
                totalRefundAmount = totalRefunded,
                note = "All affected bookings were cancelled successfully"
            });
        }

        private async Task<ResolvedSessionResult> ResolveBookingSessionAsync(CreateBookingDto dto)
        {
            if (dto.SessionID.HasValue && dto.SessionID.Value > 0)
            {
                var existingSession = await _dbContext.TrainingSessions
                    .Include(s => s.Coach)
                    .FirstOrDefaultAsync(s => s.SessionID == dto.SessionID.Value);

                return existingSession == null
                    ? new ResolvedSessionResult { ErrorResult = NotFound(new { error = "Session not found" }) }
                    : new ResolvedSessionResult { Session = existingSession, AutoCreatedSession = false };
            }

            if (!dto.CoachID.HasValue || !dto.SessionDate.HasValue || string.IsNullOrWhiteSpace(dto.StartTime))
            {
                return new ResolvedSessionResult
                {
                    ErrorResult = BadRequest(new
                    {
                        error = "Provide either SessionID or CoachID, SessionDate and StartTime to create a booking"
                    })
                };
            }

            var coach = await _dbContext.Coaches
                .Include(c => c.CoachLocations)
                .FirstOrDefaultAsync(c => c.CoachID == dto.CoachID.Value || c.UserId == dto.CoachID.Value);

            if (coach == null)
            {
                return new ResolvedSessionResult { ErrorResult = NotFound(new { error = "Coach not found" }) };
            }

            if (coach.VerificationStatus != VerificationStatus.Verified && coach.VerificationStatus != VerificationStatus.Approved)
            {
                return new ResolvedSessionResult { ErrorResult = BadRequest(new { error = "Coach must be verified before accepting bookings" }) };
            }

            var requestedSportId = dto.SportID;
            if (!requestedSportId.HasValue)
            {
                var coachSportIds = await _dbContext.CoachSports
                    .Where(cs => cs.CoachID == coach.CoachID)
                    .Select(cs => cs.SportID)
                    .Distinct()
                    .ToListAsync();

                if (coachSportIds.Count == 1)
                {
                    requestedSportId = coachSportIds[0];
                }
                else
                {
                    return new ResolvedSessionResult { ErrorResult = BadRequest(new { error = "SportID is required when the coach offers more than one sport" }) };
                }
            }

            var coachCanTeachSport = await _dbContext.CoachSports
                .AnyAsync(cs => cs.CoachID == coach.CoachID && cs.SportID == requestedSportId.Value);

            if (!coachCanTeachSport)
            {
                return new ResolvedSessionResult { ErrorResult = BadRequest(new { error = "Coach does not offer this sport" }) };
            }

            var startTime = ParseFlexibleTime(dto.StartTime);
            if (!startTime.HasValue)
            {
                return new ResolvedSessionResult { ErrorResult = BadRequest(new { error = "Invalid start time format" }) };
            }

            var endTime = ParseFlexibleTime(dto.EndTime) ?? startTime.Value.Add(TimeSpan.FromHours(1));
            if (endTime <= startTime)
            {
                return new ResolvedSessionResult { ErrorResult = BadRequest(new { error = "End time must be after start time" }) };
            }

            var requestedDurationMinutes = (endTime - startTime.Value).TotalMinutes;
            if (requestedDurationMinutes < 45 || requestedDurationMinutes > 60)
            {
                return new ResolvedSessionResult { ErrorResult = BadRequest(new { error = "Session duration must be between 45 and 60 minutes" }) };
            }

            var sessionDate = dto.SessionDate.Value.Date;
            if (sessionDate < DateTime.UtcNow.Date || (sessionDate == DateTime.UtcNow.Date && startTime.Value <= DateTime.UtcNow.TimeOfDay))
            {
                return new ResolvedSessionResult { ErrorResult = BadRequest(new { error = "Cannot book a past time slot" }) };
            }

            var availability = ParseAvailability(coach.AvailabilityStatus);
            var selectedDay = sessionDate.DayOfWeek.ToString();
            if (availability.AvailableDays.Any() && !availability.AvailableDays.Any(day => day.Equals(selectedDay, StringComparison.OrdinalIgnoreCase)))
            {
                return new ResolvedSessionResult { ErrorResult = BadRequest(new { error = "Coach is not available on the selected day" }) };
            }

            var selectedSlot = availability.DayHourSlots
                .FirstOrDefault(slot => slot.Day.Equals(selectedDay, StringComparison.OrdinalIgnoreCase));
            var allowedHours = (selectedSlot?.Hours?.Any() == true ? selectedSlot.Hours : availability.AvailableHours) ?? new List<string>();
            if (allowedHours.Any() && !allowedHours.Any(hour => TimesMatch(hour, startTime.Value)))
            {
                return new ResolvedSessionResult { ErrorResult = BadRequest(new { error = "Coach is not available at the selected hour" }) };
            }

            var overlappingSession = await _dbContext.TrainingSessions
                .Include(s => s.Coach)
                .Where(s => s.CoachID == coach.CoachID &&
                            s.SessionDate.Date == sessionDate &&
                            s.Status != SessionStatus.Cancelled &&
                            ((startTime.Value >= s.Start_Time && startTime.Value < s.End_Time) ||
                             (endTime > s.Start_Time && endTime <= s.End_Time) ||
                             (startTime.Value <= s.Start_Time && endTime >= s.End_Time)))
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.Start_Time)
                .FirstOrDefaultAsync();

            if (overlappingSession != null)
            {
                return new ResolvedSessionResult { Session = overlappingSession, AutoCreatedSession = false };
            }

            var newSession = new TrainingSession
            {
                CoachID = coach.CoachID,
                Coach = coach,
                SportID = requestedSportId.Value,
                SessionDate = sessionDate,
                SessionType = dto.SessionType ?? "Private Session",
                Location = dto.Location ?? coach.CoachLocations.Select(cl => cl.WorkingLocation).FirstOrDefault(),
                MaxParticipants = dto.MaxParticipants ?? 1,
                Start_Time = startTime.Value,
                End_Time = endTime,
                Status = SessionStatus.Scheduled
            };

            _dbContext.TrainingSessions.Add(newSession);
            await _dbContext.SaveChangesAsync();

            return new ResolvedSessionResult { Session = newSession, AutoCreatedSession = true };
        }

        private async Task TrySendNotificationAsync(int userId, string title, string message, NotificationType type)
        {
            try
            {
                await _notificationService.SendNotificationAsync(userId, title, message, type);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notification sending failed after booking-related state change for user {UserId}", userId);
            }
        }

        private async Task<int> GetActiveBookingCountAsync(int sessionId)
        {
            return await _dbContext.Bookings
                .CountAsync(b => b.SessionID == sessionId &&
                                 (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
        }

        private async Task RemoveClientSessionLinkAsync(int clientId, int sessionId)
        {
            var link = await _dbContext.ClientSessions
                .FirstOrDefaultAsync(cs => cs.ClientID == clientId && cs.SessionID == sessionId);

            if (link != null)
            {
                _dbContext.ClientSessions.Remove(link);
            }
        }

        private async Task MarkPendingPaymentAsFailedAsync(int bookingId, string reason)
        {
            var pendingPayments = await _dbContext.Payments
                .Where(p => p.BookingID == bookingId && p.Status == PaymentStatus.Pending)
                .ToListAsync();

            foreach (var pendingPayment in pendingPayments)
            {
                pendingPayment.Status = PaymentStatus.Failed;
                pendingPayment.RefundReason = reason;
            }
        }

        private async Task<decimal?> GetSessionPriceAsync(int coachId, int sportId)
        {
            return await _dbContext.CoachSports
                .Where(cs => cs.CoachID == coachId && cs.SportID == sportId)
                .Select(cs => cs.PricePerSession)
                .FirstOrDefaultAsync();
        }

        private static TimeSpan? ParseFlexibleTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (TimeSpan.TryParse(value, out var parsedTimeSpan))
            {
                return parsedTimeSpan;
            }

            var formats = new[] { "h:mm tt", "hh:mm tt", "h:mmtt", "hh:mmtt", "H:mm", "HH:mm" };
            if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime))
            {
                return parsedDateTime.TimeOfDay;
            }

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDateTime)
                ? parsedDateTime.TimeOfDay
                : null;
        }

        private static bool TimesMatch(string availabilityHour, TimeSpan selectedTime)
        {
            var parsedAvailability = ParseFlexibleTime(availabilityHour);
            return parsedAvailability.HasValue && parsedAvailability.Value == selectedTime;
        }

        private static AvailabilityPayload ParseAvailability(string? availabilityStatus)
        {
            if (string.IsNullOrWhiteSpace(availabilityStatus))
            {
                return new AvailabilityPayload();
            }

            var trimmed = availabilityStatus.Trim();
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<AvailabilityPayload>(trimmed) ?? new AvailabilityPayload();
                    payload.AvailableDays ??= new List<string>();
                    payload.AvailableHours ??= new List<string>();
                    payload.DayHourSlots ??= new List<DayHourSlot>();
                    if (payload.DayHourSlots.Count == 0 && payload.AvailableDays.Count > 0)
                    {
                        payload.DayHourSlots = payload.AvailableDays.Select(day => new DayHourSlot
                        {
                            Day = day,
                            Hours = payload.AvailableHours.ToList()
                        }).ToList();
                    }
                    return payload;
                }
                catch
                {
                    return new AvailabilityPayload();
                }
            }

            var legacyDays = trimmed
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(day => !string.IsNullOrWhiteSpace(day))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AvailabilityPayload
            {
                AvailableDays = legacyDays,
                DayHourSlots = legacyDays.Select(day => new DayHourSlot
                {
                    Day = day,
                    Hours = new List<string>()
                }).ToList()
            };
        }

        private sealed class ResolvedSessionResult
        {
            public TrainingSession? Session { get; set; }
            public IActionResult? ErrorResult { get; set; }
            public bool AutoCreatedSession { get; set; }
        }

        private sealed class DayHourSlot
        {
            public string Day { get; set; } = string.Empty;
            public List<string> Hours { get; set; } = new();
        }

        private sealed class AvailabilityPayload
        {
            public List<string> AvailableDays { get; set; } = new();
            public List<string> AvailableHours { get; set; } = new();
            public List<DayHourSlot> DayHourSlots { get; set; } = new();
        }
    }
}
