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
    public class Client
    {
        [Key]
        public int ClientID { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string F_name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string L_name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public Gender? Gender { get; set; }

        public DateTime? Date_of_Birth { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(200)]
        public string? Street_name { get; set; }

        [MaxLength(50)]
        public string? Build_num { get; set; }

        [MaxLength(100)]
        public string? ID { get; set; }

        [MaxLength(500)]
        public string? URL { get; set; }

        [MaxLength(1000)]
        public string? Bio { get; set; }

        public bool IsPhoneVerified { get; set; } = false;

        public bool IsEmailVerified { get; set; } = false;

        // Navigation Properties
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; } = null!;

        public virtual ICollection<ClientPhone> ClientPhones { get; set; } = new List<ClientPhone>();
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
        public virtual ICollection<ClientSession> ClientSessions { get; set; } = new List<ClientSession>();
        public virtual ICollection<CoachClient> CoachClients { get; set; } = new List<CoachClient>();
        public virtual ICollection<ClientRecommendation> ClientRecommendations { get; set; } = new List<ClientRecommendation>();
        public virtual ICollection<ClientAdmin> ClientAdmins { get; set; } = new List<ClientAdmin>();
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
        public virtual ICollection<ClientReport> ClientReports { get; set; } = new List<ClientReport>();
    }
}
