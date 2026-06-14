using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Payments.InitiatePayment
{
    public interface IInitiatePaymentUseCase
    {
        Task<Result<object>> ExecuteAsync(InitiatePaymentCommand command);
    }
}
