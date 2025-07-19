using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeatBay.Model
{
    public class AdminActionLog
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("AdminUser")]
        public int AdminUserId { get; set; }

        public virtual User AdminUser { get; set; }

        [Required]
        public string ActionType { get; set; }  // Ej: "Deactivate User", "Delete Song"

        [Required]
        public string Description { get; set; }

        public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    }
}
