using Maranny.Application.DTOs.Sessions;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Core.Interfaces;
using Maranny.Infrastructure.Data;
using Maranny.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        public BookingsController(
            ApplicationDbContext dbContext,
            INotificationService notificationService,
            IPaymentService paymentService)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
            _paymentService = paymentService;
        }

        // Book a session
        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> BookSession(CreateBookingDto dto)
        {
            // Get current user (client)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            // Get client ID
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return NotFound(new { error = "Client profile not found" });
            }

            // Get session
            var session = await _dbContext.TrainingSessions
                .Include(s => s.Coach)
                .FirstOrDefaultAsync(s => s.SessionID == dto.SessionID);

            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }

            // Validate session is scheduled
            if (session.Status != SessionStatus.Scheduled)
            {
                return BadRequest(new { error = "Session is not available for booking" });
            }

            // Validate session is in the future
            var sessionDateTime = session.SessionDate.Add(session.Start_Time);
            if (sessionDateTime <= DateTime.UtcNow)
            {
                return BadRequest(new { error = "Cannot book past sessions" });
            }

            // Check if session is full
            var currentBookings = await _dbContext.ClientSessions
                .CountAsync(cs => cs.SessionID == dto.SessionID);

            if (currentBookings >= session.MaxParticipants)
            {
                return BadRequest(new { error = "Session is fully booked" });
            }

            // Check if client already booked this session
            var existingBooking = await _dbContext.ClientSessions
                .FirstOrDefaultAsync(cs => cs.ClientID == client.ClientID && cs.SessionID == dto.SessionID);

            if (existingBooking != null)
            {
                return BadRequest(new { error = "You have already booked this session" });
            }

            var sessionPrice = await GetSessionPriceAsync(session.CoachID, session.SportID);
            if (sessionPrice == null)
            {
                return BadRequest(new { error = "Session price is not configured for this coach and sport" });
            }

            // Check for overlapping bookings
            var overlappingBooking = await _dbContext.ClientSessions
                .Include(cs => cs.TrainingSession)
                .Where(cs => cs.ClientID == client.ClientID &&
                            cs.TrainingSession.SessionDate.Date == session.SessionDate.Date &&
                            cs.TrainingSession.Status != SessionStatus.Cancelled &&
                            ((session.Start_Time >= cs.TrainingSession.Start_Time && session.Start_Time < cs.TrainingSession.End_Time) ||
                             (session.End_Time > cs.TrainingSession.Start_Time && session.End_Time <= cs.TrainingSession.End_Time) ||
                             (session.Start_Time <= cs.TrainingSession.Start_Time && session.End_Time >= cs.TrainingSession.End_Time)))
                .FirstOrDefaultAsync();

            if (overlappingBooking != null)
            {
                return BadRequest(new { error = "You have an overlapping booking at this time" });
            }

            // Create booking
            var booking = new Booking
            {
                SessionID = dto.SessionID,
                ClientID = client.ClientID,
                BookingDate = DateTime.UtcNow,
                Status = BookingStatus.Pending
            };
            _dbContext.Bookings.Add(booking);

            // Create client-session relationship
            var clientSession = new ClientSession
            {
                ClientID = client.ClientID,
                SessionID = dto.SessionID
            };
            _dbContext.ClientSessions.Add(clientSession);

            // Track user interaction
            var interaction = new UserInteraction
            {
                UserId = userId,
                CoachId = session.CoachID,
                Type = "Booking",
                Timestamp = DateTime.UtcNow,
                Context = $"Booked session {session.SessionID}"
            };
            _dbContext.UserInteractions.Add(interaction);

            await _dbContext.SaveChangesAsync();

            await _notificationService.SendNotificationAsync(
            session.Coach.UserId,
            "New Booking",
            $"You have a new booking for {session.SessionDate:MMM dd} at {session.Start_Time}",
            Core.Enums.NotificationType.BookingConfirmation
            );

            return Ok(new
            {
                message = "Session booked successfully",
                bookingId = booking.BookingID,
                note = "Proceed to payment method selection",
                totalPrice = sessionPrice,
                bookingStatus = booking.Status.ToString()
            });
        }

        // Get my bookings
        [HttpGet("my")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetMyBookings(
            [FromQuery] string? status = null,
            [FromQuery] string? tab = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            // Get current user (client)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            // Get client ID
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return NotFound(new { error = "Client profile not found" });
            }

            // Get bookings
            var query = _dbContext.Bookings
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Coach)
                .Include(b => b.TrainingSession)
                    .ThenInclude(s => s.Sport)
                .Where(b => b.ClientID == client.ClientID);

            // Filter by status if provided
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
                await _notificationService.SendNotificationAsync(
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
            await _dbContext.SaveChangesAsync();

            var clientUserId = await _dbContext.Clients
                .Where(c => c.ClientID == booking.ClientID)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();

            if (clientUserId != 0)
            {
                await _notificationService.SendNotificationAsync(
                    clientUserId,
                    "Booking Declined",
                    $"Your booking for {booking.TrainingSession.SessionDate:MMM dd} was declined by the coach.",
                    NotificationType.BookingCancellation);
            }

            return Ok(new { message = "Booking declined successfully" });
        }

        // Cancel booking
        [HttpPut("{bookingId}/cancel")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> CancelBooking(int bookingId, [FromQuery] string? reason = null)
        {
            // Get current user (client)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            // Get client ID
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
            {
                return NotFound(new { error = "Client profile not found" });
            }

            // Get booking with session and payment details
            var booking = await _dbContext.Bookings
                .Include(b => b.TrainingSession)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId);

            if (booking == null)
            {
                return NotFound(new { error = "Booking not found" });
            }

            // Verify ownership
            if (booking.ClientID != client.ClientID)
            {
                return Forbid();
            }

            // Validate booking can be cancelled
            if (booking.Status == BookingStatus.Cancelled)
            {
                return BadRequest(new { error = "Booking is already cancelled" });
            }

            if (booking.Status == BookingStatus.Completed)
            {
                return BadRequest(new { error = "Cannot cancel completed booking" });
            }

            // Calculate session start time
            var sessionStartDateTime = booking.TrainingSession.SessionDate.Add(booking.TrainingSession.Start_Time);

            // Check if session already started
            if (sessionStartDateTime <= DateTime.UtcNow)
            {
                return BadRequest(new { error = "Cannot cancel booking for sessions that have already started" });
            }

            // Calculate hours until session
            var hoursUntilSession = (sessionStartDateTime - DateTime.UtcNow).TotalHours;

            // Update booking status
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.CancellationReason = reason ?? "Cancelled by client";
            booking.CancelledByCoach = false;

            // Process refund if payment was made
            var payment = await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.BookingID == bookingId && p.Status == PaymentStatus.Completed);

            string refundMessage = "";

            if (payment != null)
            {
                decimal refundAmount = 0;
                string refundReason = "";

                // REFUND POLICY: 24-hour window
                if (hoursUntilSession >= 24)
                {
                    // Cancelled more than 24 hours before: 90% refund (platform keeps 10% service fee)
                    refundAmount = payment.Amount * 0.90m;
                    refundReason = $"Cancelled {hoursUntilSession:F1} hours before session. 90% refund issued (10% service fee retained).";
                    refundMessage = $"Refund of {refundAmount:F2} EGP will be processed (90% of payment). 10% service fee retained.";
                }
                else
                {
                    // Cancelled less than 24 hours before: No refund
                    refundAmount = 0;
                    refundReason = $"Cancelled only {hoursUntilSession:F1} hours before session. No refund as per cancellation policy.";
                    refundMessage = "No refund issued. Cancellation was within 24 hours of session start.";
                }

                // Process refund if applicable
                if (refundAmount > 0)
                {
                    await _paymentService.ProcessRefundAsync(payment.PaymentID, refundAmount, refundReason);
                }
                else
                {
                    // Mark payment as non-refundable
                    payment.RefundReason = refundReason;
                    await _dbContext.SaveChangesAsync();
                }
            }

            await _dbContext.SaveChangesAsync();

            // Notify coach about cancellation
            var coachUserId = await _dbContext.Coaches
                .Where(c => c.CoachID == booking.TrainingSession.CoachID)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();

            if (coachUserId != 0)
            {
                await _notificationService.SendNotificationAsync(
                    coachUserId,
                    "Booking Cancelled",
                    $"A booking for {booking.TrainingSession.SessionDate:MMM dd} has been cancelled by the client",
                    Core.Enums.NotificationType.BookingCancellation
                );
            }

            return Ok(new
            {
                message = "Booking cancelled successfully",
                refundInfo = refundMessage,
                hoursUntilSession = hoursUntilSession
            });
        }

        // Coach cancels session - Full refund to all clients
        [HttpPut("session/{sessionId}/cancel-by-coach")]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> CoachCancelSession(int sessionId, [FromQuery] string? reason = null)
        {
            // Get current user (coach)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            // Get coach ID
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            if (coach == null)
            {
                return NotFound(new { error = "Coach profile not found" });
            }

            // Get session
            var session = await _dbContext.TrainingSessions
                .FirstOrDefaultAsync(s => s.SessionID == sessionId);

            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }

            // Verify ownership
            if (session.CoachID != coach.CoachID)
            {
                return Forbid();
            }

            // Check if session can be cancelled
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

            // Get all bookings for this session
            var bookings = await _dbContext.Bookings
                .Where(b => b.SessionID == sessionId &&
                           (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                .ToListAsync();

            int refundedCount = 0;
            decimal totalRefunded = 0;

            // Cancel all bookings and issue FULL refunds
            foreach (var booking in bookings)
            {
                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = DateTime.UtcNow;
                booking.CancellationReason = reason ?? "Cancelled by coach";
                booking.CancelledByCoach = true;

                // Process FULL refund (100%) for coach cancellations
                var payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.BookingID == booking.BookingID &&
                                             p.Status == PaymentStatus.Completed);

                if (payment != null)
                {
                    decimal fullRefund = payment.Amount; // 100% refund
                    await _paymentService.ProcessRefundAsync(
                        payment.PaymentID,
                        fullRefund,
                        "Coach cancelled session. Full refund issued."
                    );

                    refundedCount++;
                    totalRefunded += fullRefund;

                    // Notify client about cancellation and refund
                    var clientUserId = await _dbContext.Clients
                        .Where(c => c.ClientID == booking.ClientID)
                        .Select(c => c.UserId)
                        .FirstOrDefaultAsync();

                    if (clientUserId != 0)
                    {
                        await _notificationService.SendNotificationAsync(
                            clientUserId,
                            "Session Cancelled by Coach",
                            $"Your session on {session.SessionDate:MMM dd} has been cancelled. Full refund of {fullRefund:F2} EGP will be processed.",
                            Core.Enums.NotificationType.BookingCancellation
                        );
                    }
                }
            }

            // Cancel the session
            session.Status = SessionStatus.Cancelled;
            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Session cancelled successfully",
                bookingsCancelled = bookings.Count,
                refundsIssued = refundedCount,
                totalRefundAmount = totalRefunded,
                note = "All clients received full refunds (100%)"
            });
        }

        private async Task<decimal?> GetSessionPriceAsync(int coachId, int sportId)
        {
            return await _dbContext.CoachSports
                .Where(cs => cs.CoachID == coachId && cs.SportID == sportId)
                .Select(cs => cs.PricePerSession)
                .FirstOrDefaultAsync();
        }
    }
}


