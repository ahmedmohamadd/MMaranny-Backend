using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Payments.GetMyPayments
{
    public sealed class GetMyPaymentsUseCase : IGetMyPaymentsUseCase
    {
        private readonly IPaymentReadRepository _payments;

        public GetMyPaymentsUseCase(IPaymentReadRepository payments)
        {
            _payments = payments;
        }

        public async Task<Result<object>> ExecuteAsync(GetMyPaymentsQuery query)
        {
            var data = await _payments.GetClientPaymentsAsync(query.UserId);

            return data == null
                ? Result<object>.Failure(new Error("Client.NotFound", "Client profile not found", ErrorType.NotFound))
                : Result<object>.Success(data);
        }
    }
}
