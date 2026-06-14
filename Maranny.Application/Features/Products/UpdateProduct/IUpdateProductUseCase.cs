using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Products.UpdateProduct
{
    public interface IUpdateProductUseCase
    {
        Task<Result<string>> ExecuteAsync(UpdateProductCommand command);
    }
}
