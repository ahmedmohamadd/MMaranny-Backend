using System.ComponentModel.DataAnnotations;

namespace Maranny.Application.DTOs.Profile
{
    public class UpdateCoachSetupDto
    {
        [MaxLength(200)]
        public string? FullName { get; set; }

        [MaxLength(100)]
        public string? NationalId { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [Range(0.01, 1000000)]
        public decimal? SessionPrice { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one sport is required")]
        public List<CoachSportSetupItemDto> Sports { get; set; } = new();

        [Required]
        [MinLength(1, ErrorMessage = "At least one location is required")]
        public List<string> Locations { get; set; } = new();

        [Required]
        [MinLength(1, ErrorMessage = "At least one available day is required")]
        public List<string> AvailableDays { get; set; } = new();

        [Required]
        [MinLength(1, ErrorMessage = "At least one available hour is required")]
        public List<string> AvailableHours { get; set; } = new();

        [MaxLength(1000)]
        public string? Bio { get; set; }

        [Range(0, 60)]
        public int? ExperienceYears { get; set; }

        [MaxLength(500)]
        public string? CertificateUrl { get; set; }
    }

    public class CoachSportSetupItemDto
    {
        [Required]
        public int SportID { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Range(0.01, 1000000)]
        public decimal? PricePerSession { get; set; }

        [Range(0, 60)]
        public int? ExperienceYears { get; set; }
    }
}
