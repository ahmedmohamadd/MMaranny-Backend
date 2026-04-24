using Maranny.Application.DTOs.Products;
using Maranny.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> CreateProduct(CreateProductDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message, data) = await _productService.CreateAsync(userId, dto);
            if (!success) return BadRequest(new { error = message });
            return Ok(new { message, data });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int? categoryId, [FromQuery] int? sportId,
            [FromQuery] decimal? maxPrice, [FromQuery] string? condition,
            [FromQuery] string? search, [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _productService.GetAllAsync(
                categoryId, sportId, maxPrice, condition, search, page, pageSize);
            return Ok(result);
        }

        [HttpGet("{productId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductDetails(int productId)
        {
            var (success, data) = await _productService.GetByIdAsync(productId);
            if (!success) return NotFound(new { error = "Product not found" });
            return Ok(data);
        }

        [HttpPut("{productId}")]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> UpdateProduct(int productId, UpdateProductDto dto)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var (success, message) = await _productService.UpdateAsync(userId, productId, dto);
            if (message == "Forbidden") return Forbid();
            if (!success) return NotFound(new { error = message });
            return Ok(new { message });
        }

        [HttpDelete("{productId}")]
        [Authorize(Roles = "Client,Admin")]
        public async Task<IActionResult> DeleteProduct(int productId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized();

            var isAdmin = User.IsInRole("Admin");
            var (success, message) = await _productService.DeleteAsync(userId, productId, isAdmin);
            if (message == "Forbidden") return Forbid();
            if (!success) return NotFound(new { error = message });
            return Ok(new { message });
        }
    }
}