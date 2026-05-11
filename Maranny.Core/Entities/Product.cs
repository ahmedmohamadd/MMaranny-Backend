using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maranny.Core.Entities
{
    public class Product
    {
        [Key]
        public int ProductID { get; set; }

        [Required]
        public int ClientID { get; set; }

        [Required]
        public int CategoryID { get; set; }

        [Required]
        [MaxLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        [MaxLength(100)]
        public string? Condition { get; set; }

        [MaxLength(100)]
        public string? ID { get; set; }

        [MaxLength(500)]
        public string? URL { get; set; }

        [MaxLength(200)]
        public string? ListingLocation { get; set; }

        public bool ShowPhoneNumber { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey(nameof(ClientID))]
        public virtual Client Client { get; set; } = null!;

        [ForeignKey(nameof(CategoryID))]
        public virtual Category Category { get; set; } = null!;

        public virtual ICollection<SportProduct> SportProducts { get; set; } = new List<SportProduct>();
        public virtual ICollection<AdminProduct> AdminProducts { get; set; } = new List<AdminProduct>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
