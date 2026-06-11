using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnLapTrinhWeb.Models
{
    public class Contact
    {
        [Key]
        public int Id { get; set; }

        public int SenderId { get; set; }
        
        [ForeignKey("SenderId")]
        public virtual AppUser? Sender { get; set; }

        public int ReceiverId { get; set; }
        
        [ForeignKey("ReceiverId")]
        public virtual AppUser? Receiver { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;
    }
}
