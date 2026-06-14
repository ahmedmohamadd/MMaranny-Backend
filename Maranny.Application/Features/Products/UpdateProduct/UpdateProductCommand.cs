using Maranny.Application.DTOs.Products;

namespace Maranny.Application.Features.Products.UpdateProduct
{
    public sealed record UpdateProductCommand(int UserId, int ProductId, UpdateProductDto Product);
}
