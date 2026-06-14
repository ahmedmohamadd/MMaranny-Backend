using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;

namespace Maranny.Application.Features.Products.UpdateProduct
{
    public sealed class UpdateProductUseCase : IUpdateProductUseCase
    {
        private readonly IClientRepository _clients;
        private readonly ICategoryRepository _categories;
        private readonly IProductRepository _products;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateProductUseCase(
            IClientRepository clients,
            ICategoryRepository categories,
            IProductRepository products,
            IUnitOfWork unitOfWork)
        {
            _clients = clients;
            _categories = categories;
            _products = products;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<string>> ExecuteAsync(UpdateProductCommand command)
        {
            var client = await _clients.GetByUserIdAsync(command.UserId);
            if (client == null)
            {
                return Failure("Client profile not found");
            }

            var product = await _products.GetByIdAsync(command.ProductId);
            if (product == null)
            {
                return Failure("Product not found");
            }

            if (product.ClientID != client.ClientID)
            {
                return Failure("Forbidden");
            }

            if (!string.IsNullOrWhiteSpace(command.Product.ProductName))
            {
                product.ProductName = command.Product.ProductName;
            }

            if (!string.IsNullOrWhiteSpace(command.Product.Description))
            {
                product.Description = command.Product.Description;
            }

            if (command.Product.Price.HasValue)
            {
                product.Price = command.Product.Price.Value;
            }

            if (!string.IsNullOrWhiteSpace(command.Product.Condition))
            {
                product.Condition = command.Product.Condition;
            }

            if (!string.IsNullOrWhiteSpace(command.Product.ImageUrl))
            {
                product.ID = command.Product.ImageUrl;
            }

            if (command.Product.CategoryID.HasValue &&
                await _categories.ExistsAsync(command.Product.CategoryID.Value))
            {
                product.CategoryID = command.Product.CategoryID.Value;
            }

            await _unitOfWork.SaveChangesAsync();
            return Result<string>.Success("Product updated successfully");
        }

        private static Result<string> Failure(string message)
        {
            return Result<string>.Failure(new Error("Product.UpdateFailed", message, ErrorType.Failure));
        }
    }
}
