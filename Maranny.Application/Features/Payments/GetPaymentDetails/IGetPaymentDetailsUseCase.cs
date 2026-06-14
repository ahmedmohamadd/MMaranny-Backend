using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Payments.GetPaymentDetails
{
    public interface IGetPaymentDetailsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetPaymentDetailsQuery query);
    }
}
