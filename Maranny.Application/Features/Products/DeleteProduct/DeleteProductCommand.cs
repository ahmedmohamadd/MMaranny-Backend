namespace Maranny.Application.Features.Products.DeleteProduct
{
    public sealed record DeleteProductCommand(int UserId, int ProductId, bool IsAdmin);
}
