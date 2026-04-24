using System.ComponentModel.DataAnnotations;

namespace Maranny.Application.DTOs.Admin
{
    public class VerifyCoachDto
    {
        [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }
    }
}