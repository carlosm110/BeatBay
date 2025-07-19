using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BeatBay.DTOs
{
    public class UserConnectionDto
    {
        public int Id { get; set; }

        [Required]
        public int ParentSubscriptionId { get; set; }

        [Required]
        public int ChildUserId { get; set; }

        public DateTime ConnectedAt { get; set; }

        public bool IsActive { get; set; }

        // Propiedades adicionales para mostrar información relacionada
        public string? ChildUserName { get; set; }
        public string? ChildUserEmail { get; set; }
        public string? ParentPlanName { get; set; }
        public DateTime? ParentSubscriptionEndDate { get; set; }
    }
}
