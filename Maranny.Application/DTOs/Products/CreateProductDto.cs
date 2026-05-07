using System.ComponentModel.DataAnnotations;

namespace Maranny.Application.DTOs.Products
{
    public class CreateProductDto
    {
        public string? ProductName { get; set; }
        public string? Title { get; set; }
        public string? Name { get; set; }

        [MaxLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string? Description { get; set; }

        [Range(0.01, 1000000, ErrorMessage = "Price must be between 0.01 and 1,000,000")]
        public decimal? Price { get; set; }

        [MaxLength(50)]
        public string? Condition { get; set; }

        public int? CategoryID { get; set; }
        public int? CategoryId { get; set; }
        public string? Category { get; set; }
        public string? CategoryName { get; set; }
        public List<int>? SportIDs { get; set; }
        public string? SellerName { get; set; }
        public string? StoreName { get; set; }
        public string? SellerPhone { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactPhone { get; set; }
        public string? Location { get; set; }
        public string? SellerLocation { get; set; }
        public string? City { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        [MaxLength(500)]
        public string? PhotoUrl { get; set; }

        public string? GetResolvedProductName() => ProductName ?? Title ?? Name;
        public int? GetResolvedCategoryId() => CategoryID ?? CategoryId;
        public string? GetResolvedCategoryName() => Category ?? CategoryName;
        public string? GetResolvedSellerName() => SellerName ?? StoreName;
        public string? GetResolvedSellerPhone() => SellerPhone ?? PhoneNumber ?? ContactPhone;
        public string? GetResolvedLocation() => Location ?? SellerLocation ?? City;
        public string? GetResolvedImageUrl() => ImageUrl ?? PhotoUrl;
    }
}
