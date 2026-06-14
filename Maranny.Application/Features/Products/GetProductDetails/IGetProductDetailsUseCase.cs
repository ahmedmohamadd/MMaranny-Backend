using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Products.GetProductDetails
{
    public interface IGetProductDetailsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetProductDetailsQuery query);
    }
}
