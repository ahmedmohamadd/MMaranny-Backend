using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Products.GetProducts
{
    public sealed class GetProductsUseCase : IGetProductsUseCase
    {
        private readonly IProductReadRepository _products;

        public GetProductsUseCase(IProductReadRepository products)
        {
            _products = products;
        }

        public async Task<Result<object>> ExecuteAsync(GetProductsQuery query)
        {
            return Result<object>.Success(await _products.GetAllAsync(
                query.CategoryId,
                query.SportId,
                query.MaxPrice,
                query.Condition,
                query.Search,
                query.Page,
                query.PageSize));
        }
    }
}
