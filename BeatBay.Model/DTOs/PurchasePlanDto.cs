using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatBay.DTOs
{
    public class PurchasePlanDto
    {
        [Required]
        public int PlanId { get; set; }
    }

    public class ChangePlanDto
    {
        [Required]
        public int NewPlanId { get; set; }
    }

    public class AddConnectionDto
    {
        [Required]
        public int ChildUserId { get; set; }
    }

    public class RemoveConnectionDto
    {
        [Required]
        public int ChildUserId { get; set; }
    }
}
