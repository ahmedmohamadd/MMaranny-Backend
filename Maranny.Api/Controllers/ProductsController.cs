using Maranny.Application.DTOs.Products;
using Maranny.Core.Entities;
using Maranny.Core.Enums;
using Maranny.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Maranny.API.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            ApplicationDbContext dbContext,
            IWebHostEnvironment environment,
            ILogger<ProductsController> logger)
        {
            _dbContext = dbContext;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("categories")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _dbContext.Categories
                .OrderBy(c => c.CategoryName)
                .Select(c => new
                {
                    c.CategoryID,
                    id = c.CategoryID,
                    c.CategoryName,
                    name = c.CategoryName,
                    c.Description,
                    productCount = c.Products.Count
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount = categories.Count,
                categories
            });
        }

        [HttpGet("search")]
        [AllowAnonymous]
        public Task<IActionResult> SearchProducts(
            [FromQuery] string? query = null,
            [FromQuery] string? category = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] int? sportId = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? condition = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            return GetProducts(categoryId, sportId, maxPrice, condition, query, category, page, pageSize);
        }

        [HttpPost("upload-image")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Client,Coach")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> UploadProductImage([FromForm] IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest(new { error = "Image file is required" });
            }

            var imageUrl = await SaveProductImageAsync(image);

            return Ok(new
            {
                message = "Image uploaded successfully",
                imageUrl
            });
        }

        [HttpPost]
        [Authorize(Roles = "Client,Coach")]
        [Consumes("multipart/form-data", "application/json")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> CreateProduct([FromForm] CreateProductDto dto, [FromForm] IFormFile? image = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized();
                }

            if (!Request.HasFormContentType)
            {
                dto = await TryReadJsonCreateProductDtoAsync() ?? dto;
            }

            var productName = dto.GetResolvedProductName()?.Trim();
            if (string.IsNullOrWhiteSpace(productName) || productName.Length < 3)
            {
                return BadRequest(new { error = "Product title is required and must be at least 3 characters" });
            }

            if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Trim().Length < 10)
            {
                return BadRequest(new { error = "Description is required and must be at least 10 characters" });
            }

            if (!dto.Price.HasValue || dto.Price.Value <= 0)
            {
                return BadRequest(new { error = "Price is required and must be greater than zero" });
            }

            if (string.IsNullOrWhiteSpace(dto.Condition))
            {
                return BadRequest(new { error = "Condition is required" });
            }

            var showPhoneNumber = dto.ShowPhoneNumber ?? true;
            var sellerPhone = dto.GetResolvedSellerPhone()?.Trim();
            if (showPhoneNumber && string.IsNullOrWhiteSpace(sellerPhone))
            {
                return BadRequest(new { error = "Phone number is required when it is shown to buyers" });
            }

            if (showPhoneNumber && !IsValidEgyptianMobileNumber(sellerPhone))
            {
                return BadRequest(new { error = "Please enter a valid Egyptian mobile number." });
            }

            var user = await _dbContext.Users
                .Include(u => u.Client)
                .Include(u => u.Coach)
                    .ThenInclude(c => c.CoachLocations)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (user == null)
            {
                return Unauthorized();
            }

            var category = await ResolveCategoryAsync(dto.GetResolvedCategoryId(), dto.GetResolvedCategoryName());
            if (category == null)
            {
                return BadRequest(new { error = "Valid product category is required" });
            }

            var client = await EnsureSellerClientProfileAsync(user, dto);
            _logger.LogInformation(
                "CreateProduct incoming request: HasFormContentType={HasFormContentType}, FormFileCount={FormFileCount}, ContentType={ContentType}",
                Request.HasFormContentType,
                Request.HasFormContentType ? Request.Form.Files.Count : 0,
                Request.ContentType ?? string.Empty);
            var imageUrl = await ResolveImageUrlAsync(dto, image, Request.HasFormContentType ? Request.Form.Files : null);

            var product = new Product
            {
                ClientID = client.ClientID,
                ProductName = productName,
                Description = dto.Description?.Trim(),
                Price = dto.Price.Value,
                Condition = dto.Condition?.Trim(),
                CategoryID = category.CategoryID,
                ID = imageUrl,
                ShowPhoneNumber = showPhoneNumber,
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation(
                "CreateProduct before SaveChanges: ProductID={ProductID}, ProductImageUrl={ProductImageUrl}",
                product.ProductID,
                product.ID ?? string.Empty);
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation(
                "CreateProduct after SaveChanges: ProductID={ProductID}, ProductImageUrl={ProductImageUrl}",
                product.ProductID,
                product.ID ?? string.Empty);

            if (dto.SportIDs != null && dto.SportIDs.Any())
            {
                var validSportIds = await _dbContext.Sports
                    .Where(s => dto.SportIDs.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync();

                foreach (var sportId in validSportIds.Distinct())
                {
                    _dbContext.SportProducts.Add(new SportProduct
                    {
                        SportID = sportId,
                        ProductID = product.ProductID
                    });
                }

                await _dbContext.SaveChangesAsync();
            }

                        var createdProduct = await LoadProductAsync(product.ProductID);
            if (createdProduct == null)
            {
                return StatusCode(500, new { error = "Product was created but could not be loaded afterwards" });
            }

            var sellerProfiles = await LoadSellerProfilesAsync(new[] { createdProduct });

            return Ok(new
            {
                message = "Product created successfully",
                productId = product.ProductID,
                product = BuildProductPayload(createdProduct, sellerProfiles)
            });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateProduct failed unexpectedly.");
                return StatusCode(500, new
                {
                    error = "Failed to create product",
                    details = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int? categoryId = null,
            [FromQuery] int? sportId = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? condition = null,
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _dbContext.Products
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Include(p => p.Category)
                .Include(p => p.SportProducts)
                    .ThenInclude(sp => sp.Sport)
                .AsQueryable();

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryID == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var normalizedCategory = category.Trim().ToLower();
                if (normalizedCategory != "all")
                {
                    query = query.Where(p => p.Category.CategoryName.ToLower().Contains(normalizedCategory));
                }
            }

            if (sportId.HasValue)
            {
                query = query.Where(p => p.SportProducts.Any(sp => sp.SportID == sportId.Value));
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(condition))
            {
                var normalizedCondition = condition.Trim().ToLower();
                query = query.Where(p => p.Condition != null && p.Condition.ToLower() == normalizedCondition);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(p =>
                    p.ProductName.ToLower().Contains(searchLower) ||
                    (p.Description != null && p.Description.ToLower().Contains(searchLower)) ||
                    p.Category.CategoryName.ToLower().Contains(searchLower) ||
                    ((p.Client.F_name + " " + p.Client.L_name).ToLower().Contains(searchLower)) ||
                    (p.Client.City != null && p.Client.City.ToLower().Contains(searchLower)) ||
                    (p.Client.Street_name != null && p.Client.Street_name.ToLower().Contains(searchLower)) ||
                    (p.ShowPhoneNumber && p.Client.User.PhoneNumber != null && p.Client.User.PhoneNumber.ToLower().Contains(searchLower)));
            }

            query = query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductID);

            var totalCount = await query.CountAsync();

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var sellerProfiles = await LoadSellerProfilesAsync(products);

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                products = products.Select(p => BuildProductPayload(p, sellerProfiles)).ToList()
            });
        }

        [HttpGet("{productId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductDetails(int productId)
        {
            var product = await LoadProductAsync(productId);
            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            var sellerProfiles = await LoadSellerProfilesAsync(new[] { product });

            return Ok(BuildProductPayload(product, sellerProfiles));
        }

        [HttpPut("{productId:int}")]
        [Authorize(Roles = "Client,Coach")]
        [Consumes("application/json")]
        public async Task<IActionResult> UpdateProduct(int productId, [FromBody] UpdateProductDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var product = await _dbContext.Products
                .Include(p => p.Client)
                .FirstOrDefaultAsync(p => p.ProductID == productId);

            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            if (product.Client.UserId != userId.Value && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(dto.ProductName))
                product.ProductName = dto.ProductName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Description))
                product.Description = dto.Description.Trim();

            if (dto.Price.HasValue)
                product.Price = dto.Price.Value;

            if (!string.IsNullOrWhiteSpace(dto.Condition))
                product.Condition = dto.Condition.Trim();

            if (dto.CategoryID.HasValue)
            {
                var category = await _dbContext.Categories.FindAsync(dto.CategoryID.Value);
                if (category == null)
                {
                    return BadRequest(new { error = "Category not found" });
                }

                product.CategoryID = dto.CategoryID.Value;
            }

            if (!string.IsNullOrWhiteSpace(dto.ImageUrl))
                product.ID = dto.ImageUrl.Trim();

            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Product updated successfully" });
        }

        [HttpDelete("{productId:int}")]
        [Authorize(Roles = "Client,Coach,Admin")]
        public async Task<IActionResult> DeleteProduct(int productId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var product = await _dbContext.Products
                .Include(p => p.Client)
                .FirstOrDefaultAsync(p => p.ProductID == productId);

            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            if (!User.IsInRole("Admin") && product.Client.UserId != userId.Value)
            {
                return Forbid();
            }

            var sportProducts = await _dbContext.SportProducts
                .Where(sp => sp.ProductID == productId)
                .ToListAsync();

            _dbContext.SportProducts.RemoveRange(sportProducts);
            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Product deleted successfully" });
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private async Task<Category?> ResolveCategoryAsync(int? categoryId, string? categoryName)
        {
            Category? category = null;

            if (categoryId.HasValue)
            {
                category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.CategoryID == categoryId.Value);
            }

            if (category == null && !string.IsNullOrWhiteSpace(categoryName))
            {
                var normalizedName = categoryName.Trim().ToLower();
                category = await _dbContext.Categories
                    .FirstOrDefaultAsync(c => c.CategoryName.ToLower() == normalizedName);
            }

            return category;
        }

        private async Task<Client> EnsureSellerClientProfileAsync(ApplicationUser user, CreateProductDto dto)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id);

            var sellerName = dto.GetResolvedSellerName();
            var location = dto.GetResolvedLocation();
            var phone = dto.GetResolvedSellerPhone();

            if (client == null)
            {
                var firstName = user.Client?.F_name ?? user.Coach?.F_name ?? user.UserName ?? "Marketplace";
                var lastName = user.Client?.L_name ?? user.Coach?.L_name ?? "Seller";

                if (!string.IsNullOrWhiteSpace(sellerName))
                {
                    var nameParts = sellerName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    firstName = nameParts.FirstOrDefault() ?? firstName;
                    lastName = nameParts.Length > 1 ? string.Join(' ', nameParts.Skip(1)) : lastName;
                }

                client = new Client
                {
                    UserId = user.Id,
                    F_name = firstName,
                    L_name = lastName,
                    Email = user.Email ?? $"{user.Id}@maranny.local",
                    Password = "ManagedByIdentity",
                    City = location,
                    Street_name = location
                };

                _dbContext.Clients.Add(client);
                await _dbContext.SaveChangesAsync();
            }
            else if (!string.IsNullOrWhiteSpace(sellerName))
            {
                var nameParts = sellerName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                client.F_name = nameParts.FirstOrDefault() ?? client.F_name;
                client.L_name = nameParts.Length > 1 ? string.Join(' ', nameParts.Skip(1)) : client.L_name;
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                client.City = location.Trim();
                client.Street_name ??= location.Trim();
            }

            if (dto.ShowPhoneNumber != false && !string.IsNullOrWhiteSpace(phone))
            {
                user.PhoneNumber = phone.Trim();
            }

            await _dbContext.SaveChangesAsync();
            return client;
        }


        private async Task<CreateProductDto?> TryReadJsonCreateProductDtoAsync()
        {
            try
            {
                Request.EnableBuffering();
                Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;

                if (string.IsNullOrWhiteSpace(body))
                {
                    return null;
                }

                var dto = JsonSerializer.Deserialize<CreateProductDto>(
                    body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                _logger.LogInformation(
                    "CreateProduct JSON fallback parsed request body successfully. HasProductName={HasProductName}, HasCategoryId={HasCategoryId}",
                    !string.IsNullOrWhiteSpace(dto?.GetResolvedProductName()),
                    dto?.GetResolvedCategoryId().HasValue == true);

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CreateProduct JSON fallback failed to parse request body.");
                return null;
            }
        }

        private async Task<string?> ResolveImageUrlAsync(
            CreateProductDto dto,
            IFormFile? image,
            IFormFileCollection? files)
        {
            var resolvedFile = image is { Length: > 0 }
                ? image
                : files?.FirstOrDefault(candidate =>
                    candidate.Length > 0 &&
                    (
                        candidate.Name.Equals("image", StringComparison.OrdinalIgnoreCase) ||
                        candidate.Name.Equals("imageFile", StringComparison.OrdinalIgnoreCase) ||
                        candidate.Name.Equals("file", StringComparison.OrdinalIgnoreCase) ||
                        candidate.Name.Equals("productImage", StringComparison.OrdinalIgnoreCase) ||
                        candidate.Name.Equals("productImageFile", StringComparison.OrdinalIgnoreCase)
                    ));

            _logger.LogInformation(
                "ResolveImageUrlAsync: ReceivedFileIsNull={ReceivedFileIsNull}, FileName={FileName}, FileLength={FileLength}",
                resolvedFile == null,
                resolvedFile?.FileName ?? string.Empty,
                resolvedFile?.Length ?? 0);

            if (resolvedFile != null && resolvedFile.Length > 0)
            {
                var savedPath = await SaveProductImageAsync(resolvedFile);
                _logger.LogInformation(
                    "ResolveImageUrlAsync: SavedRelativePath={SavedRelativePath}",
                    savedPath);
                return savedPath;
            }

            var imageUrl = dto.GetResolvedImageUrl();
            _logger.LogInformation(
                "ResolveImageUrlAsync: No file upload received. Falling back to ImageUrl='{ImageUrl}'",
                imageUrl ?? string.Empty);
            return string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        }

        private async Task<string> SaveProductImageAsync(IFormFile image)
        {
            var uploadsRoot = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "products");
            Directory.CreateDirectory(uploadsRoot);

            var extension = Path.GetExtension(image.FileName);
            var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension;
            var fileName = $"{Guid.NewGuid():N}{safeExtension}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await image.CopyToAsync(stream);

            var relativeUrl = $"/uploads/products/{fileName}";
            _logger.LogInformation("Saved marketplace image to {ImageUrl}", relativeUrl);
            return relativeUrl;
        }

        private static bool IsValidEgyptianMobileNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = Regex.Replace(value.Trim(), @"[\s\-()]", string.Empty);
            return Regex.IsMatch(normalized, @"^(?:\+20|0)1[0125][0-9]{8}$");
        }

        private async Task<Product?> LoadProductAsync(int productId)
        {
            return await _dbContext.Products
                .Include(p => p.Client)
                    .ThenInclude(c => c.User)
                .Include(p => p.Category)
                .Include(p => p.SportProducts)
                    .ThenInclude(sp => sp.Sport)
                .FirstOrDefaultAsync(p => p.ProductID == productId);
        }

        private async Task<Dictionary<int, SellerProfile>> LoadSellerProfilesAsync(IEnumerable<Product> products)
        {
            var userIds = products
                .Select(p => p.Client.UserId)
                .Distinct()
                .ToList();

            var coaches = await _dbContext.Coaches
                .Include(c => c.CoachLocations)
                .Where(c => userIds.Contains(c.UserId))
                .Select(c => new
                {
                    c.UserId,
                    c.CoachID,
                    FullName = (c.F_name + " " + c.L_name).Trim(),
                    c.AvgRating,
                    reviewsCount = c.Reviews.Count,
                    Location = c.CoachLocations.Select(cl => cl.WorkingLocation).FirstOrDefault(),
                    Role = "Coach"
                })
                .ToListAsync();

            return coaches.ToDictionary(
                c => c.UserId,
                c => new SellerProfile
                {
                    CoachId = c.CoachID,
                    Role = c.Role,
                    Name = c.FullName,
                    Rating = c.AvgRating,
                    ReviewsCount = c.reviewsCount,
                    Location = c.Location
                });
        }

        private object BuildProductPayload(Product product, Dictionary<int, SellerProfile> sellerProfiles)
        {
            sellerProfiles.TryGetValue(product.Client.UserId, out var sellerProfile);

            var sellerName = !string.IsNullOrWhiteSpace(sellerProfile?.Name)
                ? sellerProfile!.Name
                : $"{product.Client.F_name} {product.Client.L_name}".Trim();

            var sellerLocation = sellerProfile?.Location
                ?? product.Client.City
                ?? product.Client.Street_name;

            var phone = product.ShowPhoneNumber ? product.Client.User.PhoneNumber : null;

            return new
            {
                id = product.ProductID,
                productId = product.ProductID,
                title = product.ProductName,
                name = product.ProductName,
                productName = product.ProductName,
                description = product.Description,
                price = product.Price,
                condition = product.Condition,
                imageUrl = product.ID,
                image = product.ID,
                photoUrl = product.ID,
                createdAt = product.CreatedAt,
                categoryId = product.CategoryID,
                ownerId = product.Client.UserId,
                sellerId = product.Client.UserId,
                category = product.Category.CategoryName,
                categoryName = product.Category.CategoryName,
                sellerName,
                storeName = sellerName,
                showPhoneNumber = product.ShowPhoneNumber,
                sellerPhone = phone,
                phoneNumber = phone,
                contactPhone = phone,
                sellerLocation,
                location = sellerLocation,
                city = sellerLocation,
                rating = sellerProfile?.Rating,
                reviewsCount = sellerProfile?.ReviewsCount ?? 0,
                sellerRating = sellerProfile?.Rating,
                sellerRole = sellerProfile?.Role ?? "Client",
                sellerCoachId = sellerProfile?.CoachId,
                seller = new
                {
                    id = product.Client.UserId,
                    clientId = product.Client.ClientID,
                    coachId = sellerProfile?.CoachId,
                    name = sellerName,
                    email = product.Client.User.Email,
                    showPhoneNumber = product.ShowPhoneNumber,
                    phone = phone,
                    location = sellerLocation,
                    rating = sellerProfile?.Rating,
                    reviewsCount = sellerProfile?.ReviewsCount ?? 0,
                    role = sellerProfile?.Role ?? "Client"
                },
                sports = product.SportProducts.Select(sp => new
                {
                    id = sp.SportID,
                    name = sp.Sport.Name
                }).ToList()
            };
        }

        private sealed class SellerProfile
        {
            public int? CoachId { get; set; }
            public string Role { get; set; } = "Client";
            public string? Name { get; set; }
            public decimal? Rating { get; set; }
            public int ReviewsCount { get; set; }
            public string? Location { get; set; }
        }
    }
}
