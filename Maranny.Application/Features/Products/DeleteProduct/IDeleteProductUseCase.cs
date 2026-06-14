using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Products.DeleteProduct
{
    public interface IDeleteProductUseCase
    {
        Task<Result<string>> ExecuteAsync(DeleteProductCommand command);
    }
}
