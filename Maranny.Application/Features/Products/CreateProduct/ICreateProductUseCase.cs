using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Products.CreateProduct
{
    public interface ICreateProductUseCase
    {
        Task<Result<object>> ExecuteAsync(CreateProductCommand command);
    }
}
