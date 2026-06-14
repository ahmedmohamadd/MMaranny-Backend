using Maranny.Application.DTOs.Products;
using Maranny.Application.Features.Products.CreateProduct;
using Maranny.Application.Features.Products.DeleteProduct;
using Maranny.Application.Features.Products.GetProductDetails;
using Maranny.Application.Features.Products.GetProducts;
using Maranny.Application.Features.Products.UpdateProduct;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly ICreateProductUseCase _createProductUseCase;
        private readonly IGetProductsUseCase _getProductsUseCase;
        private readonly IGetProductDetailsUseCase _getProductDetailsUseCase;
        private readonly IUpdateProductUseCase _updateProductUseCase;
        private readonly IDeleteProductUseCase _deleteProductUseCase;

        public ProductsController(
            ICreateProductUseCase createProductUseCase,
            IGetProductsUseCase getProductsUseCase,
            IGetProductDetailsUseCase getProductDetailsUseCase,
            IUpdateProductUseCase updateProductUseCase,
            IDeleteProductUseCase deleteProductUseCase)
        {
            _createProductUseCase = createProductUseCase;
            _getProductsUseCase = getProductsUseCase;
            _getProductDetailsUseCase = getProductDetailsUseCase;
            _updateProductUseCase = updateProductUseCase;
            _deleteProductUseCase = deleteProductUseCase;
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> CreateProduct(CreateProductDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _createProductUseCase.ExecuteAsync(new CreateProductCommand(userId, dto));
            if (result.IsFailure) return BadRequest(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int? categoryId, [FromQuery] int? sportId,
            [FromQuery] decimal? maxPrice, [FromQuery] string? condition,
            [FromQuery] string? search, [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _getProductsUseCase.ExecuteAsync(
                new GetProductsQuery(categoryId, sportId, maxPrice, condition, search, page, pageSize));

            return Ok(result.Value);
        }

        [HttpGet("{productId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductDetails(int productId)
        {
            var result = await _getProductDetailsUseCase.ExecuteAsync(new GetProductDetailsQuery(productId));
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(result.Value);
        }

        [HttpPut("{productId}")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> UpdateProduct(int productId, UpdateProductDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var result = await _updateProductUseCase.ExecuteAsync(new UpdateProductCommand(userId, productId, dto));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(new { message = result.Value });
        }

        [HttpDelete("{productId}")]
        [Authorize(Roles = "Client,Admin")]
        public async Task<IActionResult> DeleteProduct(int productId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var result = await _deleteProductUseCase.ExecuteAsync(new DeleteProductCommand(userId, productId, isAdmin));
            if (result.Error?.Message == "Forbidden") return Forbid();
            if (result.IsFailure) return NotFound(new { error = result.Error!.Message });
            return Ok(new { message = result.Value });
        }
    }
}
