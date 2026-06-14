using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Products.GetProducts
{
    public interface IGetProductsUseCase
    {
        Task<Result<object>> ExecuteAsync(GetProductsQuery query);
    }
}
