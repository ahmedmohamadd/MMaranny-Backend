using System.ComponentModel.DataAnnotations;

namespace Maranny.Application.DTOs.Admin
{
    public class BlockUserDto
    {
        [Required(ErrorMessage = "Reason is required")]
        [MinLength(10, ErrorMessage = "Reason must be at least 10 characters")]
        [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string Reason { get; set; } = string.Empty;
    }
}