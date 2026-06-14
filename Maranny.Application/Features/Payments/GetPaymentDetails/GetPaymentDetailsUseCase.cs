using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Payments.GetPaymentDetails
{
    public sealed class GetPaymentDetailsUseCase : IGetPaymentDetailsUseCase
    {
        private readonly IPaymentReadRepository _payments;

        public GetPaymentDetailsUseCase(IPaymentReadRepository payments)
        {
            _payments = payments;
        }

        public async Task<Result<object>> ExecuteAsync(GetPaymentDetailsQuery query)
        {
            var (error, data) = await _payments.GetPaymentDetailsAsync(query.UserId, query.PaymentId, query.IsAdmin);

            return error == null
                ? Result<object>.Success(data!)
                : Result<object>.Failure(new Error("Payment.DetailsFailed", error, ErrorType.NotFound));
        }
    }
}
