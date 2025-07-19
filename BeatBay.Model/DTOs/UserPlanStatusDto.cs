using BeatBay.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatBay.DTOs
{
    public class UserPlanStatusDto
    {
        public bool HasPlan { get; set; }
        public PlanSubscriptionDto? CurrentSubscription { get; set; }
        public bool CanPurchasePlan { get; set; }
        public string? ReasonCannotPurchase { get; set; }
        public List<PlanDto> AvailablePlans { get; set; } = new List<PlanDto>();
    }
}
