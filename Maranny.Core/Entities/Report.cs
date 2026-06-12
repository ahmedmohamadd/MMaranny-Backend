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
    public class Report
    {
        [Key]
        public int ReportID { get; set; }

        public int? ProductID { get; set; }

        public int? CoachID { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? ReporterType { get; set; }

        [MaxLength(100)]
        public string? ReportedType { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        public ReportStatus Status { get; set; } = ReportStatus.Pending;

        [MaxLength(50)]
        public string? Priority { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey(nameof(ProductID))]
        public virtual Product? Product { get; set; }

        [ForeignKey(nameof(CoachID))]
        public virtual Coach? Coach { get; set; }

        public virtual ICollection<ClientReport> ClientReports { get; set; } = new List<ClientReport>();
        public virtual ICollection<AdminReport> AdminReports { get; set; } = new List<AdminReport>();
        public virtual ICollection<ReportNotification> ReportNotifications { get; set; } = new List<ReportNotification>();
    }
}
