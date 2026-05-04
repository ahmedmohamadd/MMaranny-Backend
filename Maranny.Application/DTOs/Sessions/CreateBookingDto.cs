using System.ComponentModel.DataAnnotations;

namespace Maranny.Application.DTOs.Sessions
{
    public class CreateBookingDto
    {
        public int? SessionID { get; set; }

        public int? CoachID { get; set; }

        public int? SportID { get; set; }

        public DateTime? SessionDate { get; set; }

        [MaxLength(50)]
        public string? StartTime { get; set; }

        [MaxLength(50)]
        public string? EndTime { get; set; }

        [MaxLength(100)]
        public string? SessionType { get; set; }

        [MaxLength(500)]
        public string? Location { get; set; }

        [Range(1, 100)]
        public int? MaxParticipants { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
