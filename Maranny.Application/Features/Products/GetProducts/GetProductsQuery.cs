namespace Maranny.Application.Features.Products.GetProducts
{
    public sealed record GetProductsQuery(
        int? CategoryId,
        int? SportId,
        decimal? MaxPrice,
        string? Condition,
        string? Search,
        int Page,
        int PageSize);
}
