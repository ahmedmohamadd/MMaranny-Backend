using Maranny.Application.Abstractions.Persistence;
using Maranny.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maranny.Infrastructure.Persistence.ReadRepositories
{
    public sealed class ProductReadRepository : IProductReadRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public ProductReadRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<object> GetAllAsync(
            int? categoryId,
            int? sportId,
            decimal? maxPrice,
            string? condition,
            string? search,
            int page,
            int pageSize)
        {
            var query = _dbContext.Products
                .Include(p => p.Client)
                .Include(p => p.Category)
                .Include(p => p.SportProducts).ThenInclude(sp => sp.Sport)
                .AsQueryable();

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryID == categoryId.Value);
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
                query = query.Where(p => p.Condition!.ToLower() == condition.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.ToLower();
                query = query.Where(p => p.ProductName.ToLower().Contains(normalizedSearch) ||
                                         p.Description!.ToLower().Contains(normalizedSearch));
            }

            query = query.OrderByDescending(p => p.ProductID);
            var totalCount = await query.CountAsync();

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                })
                .ToListAsync();

            return new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                products
            };
        }

        public async Task<object?> GetByIdAsync(int productId)
        {
            var product = await _dbContext.Products
                .Include(p => p.Client).ThenInclude(c => c.User)
                .Include(p => p.Category)
                .Include(p => p.SportProducts).ThenInclude(sp => sp.Sport)
                .FirstOrDefaultAsync(p => p.ProductID == productId);

            if (product == null)
            {
                return null;
            }

            return new
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
        }
    }
}
