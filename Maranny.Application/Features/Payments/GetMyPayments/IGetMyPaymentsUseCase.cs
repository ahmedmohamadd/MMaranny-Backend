using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Payments.GetMyPayments
{
    public interface IGetMyPaymentsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetMyPaymentsQuery query);
    }
}
