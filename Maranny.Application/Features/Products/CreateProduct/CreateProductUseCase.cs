using Maranny.Application.Abstractions.Persistence;
using Maranny.Application.Common.Results;
using Maranny.Core.Entities;

namespace Maranny.Application.Features.Products.CreateProduct
{
    public sealed class CreateProductUseCase : ICreateProductUseCase
    {
        private readonly IClientRepository _clients;
        private readonly ICategoryRepository _categories;
        private readonly ISportRepository _sports;
        private readonly IProductRepository _products;
        private readonly IUnitOfWork _unitOfWork;

        public CreateProductUseCase(
            IClientRepository clients,
            ICategoryRepository categories,
            ISportRepository sports,
            IProductRepository products,
            IUnitOfWork unitOfWork)
        {
            _clients = clients;
            _categories = categories;
            _sports = sports;
            _products = products;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<object>> ExecuteAsync(CreateProductCommand command)
        {
            var client = await _clients.GetByUserIdAsync(command.UserId);
            if (client == null)
            {
                return Failure("Only clients can create product listings");
            }

            if (!await _categories.ExistsAsync(command.Product.CategoryID))
            {
                return Failure("Category not found");
            }

            var product = new Product
            {
                ClientID = client.ClientID,
                ProductName = command.Product.ProductName,
                Description = command.Product.Description,
                Price = command.Product.Price,
                Condition = command.Product.Condition,
                CategoryID = command.Product.CategoryID,
                ID = command.Product.ImageUrl
            };

            await _products.AddAsync(product);
            await _unitOfWork.SaveChangesAsync();

            if (command.Product.SportIDs != null && command.Product.SportIDs.Any())
            {
                var existingSportIds = await _sports.GetExistingIdsAsync(command.Product.SportIDs);
                foreach (var sportId in existingSportIds)
                {
                    await _products.AddSportProductAsync(new SportProduct
                    {
                        SportID = sportId,
                        ProductID = product.ProductID
                    });
                }

                await _unitOfWork.SaveChangesAsync();
            }

            return Result<object>.Success(new
            {
                message = "Product created successfully",
                data = new { productId = product.ProductID }
            });
        }

        private static Result<object> Failure(string message)
        {
            return Result<object>.Failure(new Error("Product.CreateFailed", message, ErrorType.Failure));
        }
    }
}
