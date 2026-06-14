using Maranny.Application.DTOs.Products;

namespace Maranny.Application.Features.Products.CreateProduct
{
    public sealed record CreateProductCommand(int UserId, CreateProductDto Product);
}
