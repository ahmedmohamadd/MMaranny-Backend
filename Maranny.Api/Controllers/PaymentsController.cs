using Maranny.Application.DTOs.Payments;
using Maranny.Core.Enums;
using Maranny.Core.Interfaces;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ApplicationDbContext _dbContext;
        private readonly INotificationService _notificationService;

        public PaymentsController(
            IPaymentService paymentService,
            ApplicationDbContext dbContext,
            INotificationService notificationService)
        {
            _paymentService = paymentService;
            _dbContext = dbContext;
            _notificationService = notificationService;
        }

        [HttpPost("initiate")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> InitiatePayment(InitiatePaymentDto dto)
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
                .FirstOrDefaultAsync(b => b.BookingID == dto.BookingID);

            if (booking == null)
            {
                return NotFound(new { error = "Booking not found" });
            }

            if (booking.ClientID != client.ClientID)
            {
                return Forbid();
            }

            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
            {
                return BadRequest(new { error = "Payment cannot be initiated for this booking" });
            }

            var normalizedMethod = dto.Method?.Trim();
            if (!string.Equals(normalizedMethod, "Cash", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Only Cash payment is supported in this phase" });
            }

            var expectedAmount = await _dbContext.CoachSports
                .Where(cs => cs.CoachID == booking.TrainingSession.CoachID &&
                             cs.SportID == booking.TrainingSession.SportID)
                .Select(cs => cs.PricePerSession)
                .FirstOrDefaultAsync();

            if (!expectedAmount.HasValue || expectedAmount.Value <= 0)
            {
                return BadRequest(new { error = "Session price is not configured for this coach and sport" });
            }

            if (dto.Amount.HasValue && dto.Amount.Value != expectedAmount.Value)
            {
                return BadRequest(new
                {
                    error = "Payment amount does not match the configured session price",
                    expectedAmount = expectedAmount.Value
                });
            }

            var existingPayment = await _paymentService.GetPaymentByBookingIdAsync(dto.BookingID);
            if (existingPayment != null)
            {
                return Ok(new
                {
                    message = "Cash payment already selected for this booking",
                    paymentId = existingPayment.PaymentID,
                    amount = existingPayment.Amount,
                    method = existingPayment.Method,
                    status = existingPayment.Status.ToString(),
                    bookingStatus = booking.Status.ToString(),
                    cashPayment = true,
                    paymentUrl = (string?)null
                });
            }

            try
            {
                var payment = await _paymentService.InitiatePaymentAsync(
                    dto.BookingID,
                    expectedAmount.Value,
                    NormalizePaymentMethod(normalizedMethod!),
                    client.ClientID
                );

                await _notificationService.SendNotificationAsync(
                    booking.TrainingSession.Coach.UserId,
                    "Cash Payment Selected",
                    $"A client selected cash payment for the booking on {booking.TrainingSession.SessionDate:MMM dd}.",
                    NotificationType.BookingConfirmation
                );

                return Ok(new
                {
                    message = "Cash payment selected successfully",
                    paymentId = payment.PaymentID,
                    paymentUrl = (string?)null,
                    amount = payment.Amount,
                    platformFee = payment.PlatformFee,
                    method = payment.Method,
                    status = payment.Status.ToString(),
                    bookingStatus = booking.Status.ToString(),
                    cashPayment = true,
                    note = "Client will pay cash offline."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to save cash payment selection", details = ex.Message });
            }
        }

        [HttpGet("{paymentId:int}")]
        public async Task<IActionResult> GetPaymentDetails(int paymentId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var payment = await _dbContext.Payments
                .Include(p => p.Booking)
                .Include(p => p.TrainingSession)
                    .ThenInclude(s => s.Coach)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId);

            if (payment == null)
            {
                return NotFound(new { error = "Payment not found" });
            }

            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            var coach = await _dbContext.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);

            var isOwner = (client != null && payment.ClientID == client.ClientID) ||
                          (coach != null && payment.TrainingSession.CoachID == coach.CoachID);

            if (!isOwner && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var result = new
            {
                payment.PaymentID,
                payment.BookingID,
                payment.Amount,
                payment.Method,
                Status = payment.Status.ToString(),
                payment.TransactionDate,
                payment.PlatformFee,
                payment.RefundAmount,
                cashPayment = string.Equals(payment.Method, "Cash", StringComparison.OrdinalIgnoreCase),
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
            };

            return Ok(result);
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentWebhook([FromBody] object webhookData)
        {
            try
            {
                return Ok(new { message = "Webhook received" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("my")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetMyPayments()
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

            var payments = await _dbContext.Payments
                .Include(p => p.TrainingSession)
                    .ThenInclude(s => s.Coach)
                .Where(p => p.ClientID == client.ClientID)
                .OrderByDescending(p => p.TransactionDate)
                .Select(p => new
                {
                    p.PaymentID,
                    p.Amount,
                    p.Method,
                    Status = p.Status.ToString(),
                    p.TransactionDate,
                    cashPayment = p.Method == "Cash",
                    Session = new
                    {
                        p.TrainingSession.SessionDate,
                        p.TrainingSession.Start_Time,
                        CoachName = p.TrainingSession.Coach.F_name + " " + p.TrainingSession.Coach.L_name
                    }
                })
                .ToListAsync();

            return Ok(payments);
        }

        private static string NormalizePaymentMethod(string method)
        {
            return string.Equals(method, "cash", StringComparison.OrdinalIgnoreCase)
                ? "Cash"
                : method;
        }
    }
}
