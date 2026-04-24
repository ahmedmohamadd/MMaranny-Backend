using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Products;
using Maranny.Application.Interfaces;
using Maranny.Core.Entities;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Services
{
    public class ProductsService : IProductService
    {
        private readonly ApplicationDbContext _dbContext;

        public ProductsService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(bool success, string message, object? data)> CreateAsync(int userId, CreateProductDto dto)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null)
                return (false, "Only clients can create product listings", null);

            var category = await _dbContext.Categories.FindAsync(dto.CategoryID);
            if (category == null)
                return (false, "Category not found", null);

            var product = new Product
            {
                ClientID = client.ClientID,
                ProductName = dto.ProductName,
                Description = dto.Description,
                Price = dto.Price,
                Condition = dto.Condition,
                CategoryID = dto.CategoryID,
                ID = dto.ImageUrl
            };

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            if (dto.SportIDs != null && dto.SportIDs.Any())
            {
                foreach (var sportId in dto.SportIDs)
                {
                    var sport = await _dbContext.Sports.FindAsync(sportId);
                    if (sport != null)
                        _dbContext.SportProducts.Add(new SportProduct
                        {
                            SportID = sportId,
                            ProductID = product.ProductID
                        });
                }
                await _dbContext.SaveChangesAsync();
            }

            return (true, "Product created successfully", new { productId = product.ProductID });
        }

        public async Task<object> GetAllAsync(int? categoryId, int? sportId, decimal? maxPrice,
            string? condition, string? search, int page, int pageSize)
        {
            var query = _dbContext.Products
                .Include(p => p.Client)
                .Include(p => p.Category)
                .Include(p => p.SportProducts).ThenInclude(sp => sp.Sport)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryID == categoryId.Value);
            if (sportId.HasValue)
                query = query.Where(p => p.SportProducts.Any(sp => sp.SportID == sportId.Value));
            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);
            if (!string.IsNullOrWhiteSpace(condition))
                query = query.Where(p => p.Condition!.ToLower() == condition.ToLower());
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(p => p.ProductName.ToLower().Contains(s) ||
                                         p.Description!.ToLower().Contains(s));
            }

            query = query.OrderByDescending(p => p.ProductID);
            var totalCount = await query.CountAsync();

            var products = await query
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new
                {
                    p.ProductID,
                    p.ProductName,
                    p.Description,
                    p.Price,
                    p.Condition,
                    ImageUrl = p.ID,
                    Category = new { p.Category.CategoryID, p.Category.CategoryName },
                    Seller = new { p.Client.ClientID, Name = p.Client.F_name + " " + p.Client.L_name },
                    Sports = p.SportProducts.Select(sp => new { id = sp.SportID, sp.Sport.Name }).ToList()
                }).ToListAsync();

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                products
            };
        }

        public async Task<(bool success, object? data)> GetByIdAsync(int productId)
        {
            var product = await _dbContext.Products
                .Include(p => p.Client).ThenInclude(c => c.User)
                .Include(p => p.Category)
                .Include(p => p.SportProducts).ThenInclude(sp => sp.Sport)
                .FirstOrDefaultAsync(p => p.ProductID == productId);

            if (product == null) return (false, null);

            var result = new
            {
                product.ProductID,
                product.ProductName,
                product.Description,
                product.Price,
                product.Condition,
                ImageUrl = product.ID,
                Category = new { product.Category.CategoryID, product.Category.CategoryName },
                Seller = new
                {
                    product.Client.ClientID,
                    Name = product.Client.F_name + " " + product.Client.L_name,
                    Email = product.Client.User.Email,
                    Phone = product.Client.User.PhoneNumber
                },
                Sports = product.SportProducts.Select(sp => new { id = sp.SportID, sp.Sport.Name }).ToList()
            };

            return (true, result);
        }

        public async Task<(bool success, string message)> UpdateAsync(int userId, int productId, UpdateProductDto dto)
        {
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) return (false, "Client profile not found");

            var product = await _dbContext.Products.FindAsync(productId);
            if (product == null) return (false, "Product not found");
            if (product.ClientID != client.ClientID) return (false, "Forbidden");

            if (!string.IsNullOrWhiteSpace(dto.ProductName)) product.ProductName = dto.ProductName;
            if (!string.IsNullOrWhiteSpace(dto.Description)) product.Description = dto.Description;
            if (dto.Price.HasValue) product.Price = dto.Price.Value;
            if (!string.IsNullOrWhiteSpace(dto.Condition)) product.Condition = dto.Condition;
            if (!string.IsNullOrWhiteSpace(dto.ImageUrl)) product.ID = dto.ImageUrl;
            if (dto.CategoryID.HasValue)
            {
                var category = await _dbContext.Categories.FindAsync(dto.CategoryID.Value);
                if (category != null) product.CategoryID = dto.CategoryID.Value;
            }

            await _dbContext.SaveChangesAsync();
            return (true, "Product updated successfully");
        }

        public async Task<(bool success, string message)> DeleteAsync(int userId, int productId, bool isAdmin)
        {
            var product = await _dbContext.Products.FindAsync(productId);
            if (product == null) return (false, "Product not found");

            if (!isAdmin)
            {
                var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null || product.ClientID != client.ClientID)
                    return (false, "Forbidden");
            }

            var sportProducts = await _dbContext.SportProducts
                .Where(sp => sp.ProductID == productId).ToListAsync();
            _dbContext.SportProducts.RemoveRange(sportProducts);
            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();

            return (true, "Product deleted successfully");
        }
    }
}