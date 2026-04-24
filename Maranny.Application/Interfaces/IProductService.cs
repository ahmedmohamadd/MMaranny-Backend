using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Maranny.Application.DTOs.Products;

namespace Maranny.Application.Interfaces
{
    public interface IProductService
    {
        Task<(bool success, string message, object? data)> CreateAsync(int userId, CreateProductDto dto);
        Task<object> GetAllAsync(int? categoryId, int? sportId, decimal? maxPrice, string? condition, string? search, int page, int pageSize);
        Task<(bool success, object? data)> GetByIdAsync(int productId);
        Task<(bool success, string message)> UpdateAsync(int userId, int productId, UpdateProductDto dto);
        Task<(bool success, string message)> DeleteAsync(int userId, int productId, bool isAdmin);
    }
}