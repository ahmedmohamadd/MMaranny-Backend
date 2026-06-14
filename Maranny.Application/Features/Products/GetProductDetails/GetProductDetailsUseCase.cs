using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Products.GetProductDetails
{
    public sealed class GetProductDetailsUseCase : IGetProductDetailsUseCase
    {
        private readonly IProductReadRepository _products;

        public GetProductDetailsUseCase(IProductReadRepository products)
        {
            _products = products;
        }

        public async Task<Result<object>> ExecuteAsync(GetProductDetailsQuery query)
        {
            var product = await _products.GetByIdAsync(query.ProductId);

            return product == null
                ? Result<object>.Failure(new Error("Product.NotFound", "Product not found", ErrorType.NotFound))
                : Result<object>.Success(product);
        }
    }
}
