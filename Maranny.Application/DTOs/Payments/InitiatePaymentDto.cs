using System.ComponentModel.DataAnnotations;

namespace Maranny.Application.DTOs.Payments
{
    public class InitiatePaymentDto
    {
        [Required(ErrorMessage = "Booking ID is required")]
        public int BookingID { get; set; }

        [Range(0.01, 1000000, ErrorMessage = "Amount must be positive")]
        public decimal? Amount { get; set; }

        [Required(ErrorMessage = "Payment method is required")]
        [MaxLength(50)]
        public string Method { get; set; } = string.Empty; // "Cash" for this phase

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
