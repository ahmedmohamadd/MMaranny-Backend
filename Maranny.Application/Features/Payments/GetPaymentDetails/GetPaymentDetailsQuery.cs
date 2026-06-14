namespace Maranny.Application.Features.Payments.GetPaymentDetails
{
    public sealed record GetPaymentDetailsQuery(int UserId, int PaymentId, bool IsAdmin);
}
