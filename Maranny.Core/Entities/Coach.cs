using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Maranny.Core.Enums;

namespace Maranny.Core.Entities
{
    public class Coach
    {
        [Key]
        public int CoachID { get; set; }

        [Required]
        public int UserId { get; set; }

        public string? AvailabilityStatus { get; set; }

        [MaxLength(1000)]
        public string? Bio { get; set; }

        [Required]
        [MaxLength(100)]
        public string F_name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string L_name { get; set; } = string.Empty;

        public int? ExperienceYears { get; set; }

        public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;

        [MaxLength(500)]
        public string? CertificateUrl { get; set; }

        public DateTime? VerifiedAt { get; set; }

        public int? VerifiedByAdminId { get; set; }

        [MaxLength(500)]
        public string? VerificationNotes { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        [MaxLength(100)]
        public string? BankAccountNo { get; set; }

        [MaxLength(100)]
        public string? ID { get; set; }

        [MaxLength(500)]
        public string? URL { get; set; }

        [Column(TypeName = "decimal(3,2)")]
        public decimal? AvgRating { get; set; }

        public Gender? Gender { get; set; }

        [Required(ErrorMessage = "National ID is required")]
        [MaxLength(500)]
        public string NationalIdImageUrl { get; set; } = string.Empty;

        public bool IsCertified { get; set; } = false;

        [MaxLength(500)]
        public string? CertificateImageUrl { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; } = null!;

        public virtual ICollection<CoachSport> CoachSports { get; set; } = new List<CoachSport>();
        public virtual ICollection<TrainingSession> TrainingSessions { get; set; } = new List<TrainingSession>();
        public virtual ICollection<CoachClient> CoachClients { get; set; } = new List<CoachClient>();
        public virtual ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
        public virtual ICollection<CoachLocation> CoachLocations { get; set; } = new List<CoachLocation>();
        public virtual ICollection<CoachAdmin> CoachAdmins { get; set; } = new List<CoachAdmin>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
