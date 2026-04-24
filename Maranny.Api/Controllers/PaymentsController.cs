using Maranny.Application.DTOs.Payments;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentsManagementService _paymentsService;

        public PaymentsController(IPaymentsManagementService paymentsService)
        {
            _paymentsService = paymentsService;
        }

        [HttpPost("initiate")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> InitiatePayment(InitiatePaymentDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _paymentsService.InitiatePaymentAsync(userId, dto);
            if (message == "Forbidden") return Forbid();
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message, data });
        }

        [HttpGet("{paymentId:int}")]
        public async Task<IActionResult> GetPaymentDetails(int paymentId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var (success, message, data) = await _paymentsService.GetPaymentDetailsAsync(userId, paymentId, isAdmin);
            if (message == "Forbidden") return Forbid();
            if (!success) return NotFound(new { error = message });
            return Ok(data);
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public IActionResult PaymentWebhook([FromBody] object webhookData)
        {
            return Ok(new { message = "Webhook received" });
        }

        [HttpGet("my")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> GetMyPayments()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, data) = await _paymentsService.GetMyPaymentsAsync(userId);
            if (!success) return NotFound(new { error = "Client profile not found" });
            return Ok(data);
        }
    }
}