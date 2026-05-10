using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maranny.Core.Entities
{
    public class ChatMessage
    {
        [Key]
        public int MessageID { get; set; }

        [Required]
        public int SenderID { get; set; } // UserId of sender

        [Required]
        public int ReceiverID { get; set; } // UserId of receiver

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        [Required]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }

        // Optional: Message type (text, image, file)
        [MaxLength(50)]
        public string MessageType { get; set; } = "text";

        [MaxLength(500)]
        public string? AttachmentUrl { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(SenderID))]
        public virtual ApplicationUser Sender { get; set; } = null!;

        [ForeignKey(nameof(ReceiverID))]
        public virtual ApplicationUser Receiver { get; set; } = null!;
    }
}
