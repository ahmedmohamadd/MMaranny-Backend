using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Products.DeleteProduct
{
    public sealed class DeleteProductUseCase : IDeleteProductUseCase
    {
        private readonly IClientRepository _clients;
        private readonly IProductRepository _products;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteProductUseCase(
            IClientRepository clients,
            IProductRepository products,
            IUnitOfWork unitOfWork)
        {
            _clients = clients;
            _products = products;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<string>> ExecuteAsync(DeleteProductCommand command)
        {
            var product = await _products.GetByIdAsync(command.ProductId);
            if (product == null)
            {
                return Failure("Product not found");
            }

            if (!command.IsAdmin)
            {
                var client = await _clients.GetByUserIdAsync(command.UserId);
                if (client == null || product.ClientID != client.ClientID)
                {
                    return Failure("Forbidden");
                }
            }

            await _products.RemoveProductSportsAsync(command.ProductId);
            _products.Remove(product);
            await _unitOfWork.SaveChangesAsync();

            return Result<string>.Success("Product deleted successfully");
        }

        private static Result<string> Failure(string message)
        {
            return Result<string>.Failure(new Error("Product.DeleteFailed", message, ErrorType.Failure));
        }
    }
}
