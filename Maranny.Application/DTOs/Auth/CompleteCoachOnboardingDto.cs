using System.ComponentModel.DataAnnotations;

namespace Maranny.Application.DTOs.Auth
{
    public class CompleteCoachOnboardingDto : IValidatableObject
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string NationalId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [Range(0, 60)]
        public int ExperienceYears { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        public decimal SessionPrice { get; set; }

        [Required]
        [MinLength(1)]
        public List<CoachOnboardingSportDto> Sports { get; set; } = new();

        [Required]
        [MinLength(1)]
        public List<string> AvailableDays { get; set; } = new();

        public List<string> AvailableHours { get; set; } = new();

        public List<CoachAvailabilitySlotDto> DayHourSlots { get; set; } = new();

        [MaxLength(1000)]
        public string? Bio { get; set; }

        [MaxLength(500)]
        public string? CertificateUrl { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrWhiteSpace(Bio) && CountWords(Bio) < 20)
            {
                yield return new ValidationResult(
                    "Bio is optional, but if added it must contain at least 20 words.",
                    new[] { nameof(Bio) });
            }
        }

        private static int CountWords(string value)
        {
            return value
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }
    }

    public class CoachOnboardingSportDto
    {
        [Required]
        public int SportID { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }
    }

    public class CoachAvailabilitySlotDto
    {
        [Required]
        [MaxLength(50)]
        public string Day { get; set; } = string.Empty;

        [MinLength(1)]
        public List<string> Hours { get; set; } = new();
    }
}
