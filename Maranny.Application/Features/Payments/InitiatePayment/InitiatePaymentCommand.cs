using Maranny.Application.DTOs.Payments;

namespace Maranny.Application.Features.Payments.InitiatePayment
{
    public sealed record InitiatePaymentCommand(int UserId, InitiatePaymentDto Payment);
}
