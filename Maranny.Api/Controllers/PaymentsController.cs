using Maranny.Application.DTOs.Payments;
using Maranny.Application.Features.Payments.GetMyPayments;
using Maranny.Application.Features.Payments.GetPaymentDetails;
using Maranny.Application.Features.Payments.InitiatePayment;
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
        private readonly IInitiatePaymentUseCase _initiatePaymentUseCase;
        private readonly IGetPaymentDetailsUseCase _getPaymentDetailsUseCase;
        private readonly IGetMyPaymentsUseCase _getMyPaymentsUseCase;

        public PaymentsController(
            IInitiatePaymentUseCase initiatePaymentUseCase,
            IGetPaymentDetailsUseCase getPaymentDetailsUseCase,
            IGetMyPaymentsUseCase getMyPaymentsUseCase)
        {
            _initiatePaymentUseCase = initiatePaymentUseCase;
            _getPaymentDetailsUseCase = getPaymentDetailsUseCase;
            _getMyPaymentsUseCase = getMyPaymentsUseCase;
        }

        [HttpPost("initiate")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> InitiatePayment(InitiatePaymentDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _initiatePaymentUseCase.ExecuteAsync(new InitiatePaymentCommand(userId, dto));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpGet("{paymentId:int}")]
        public async Task<IActionResult> GetPaymentDetails(int paymentId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var result = await _getPaymentDetailsUseCase.ExecuteAsync(
                new GetPaymentDetailsQuery(userId, paymentId, isAdmin));

            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(result.Value);
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

            var result = await _getMyPaymentsUseCase.ExecuteAsync(new GetMyPaymentsQuery(userId));
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(result.Value);
        }
    }
}
