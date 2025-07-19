using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatBay.Model
{
    public class UserConnection
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("ParentSubscription")]
        public int ParentSubscriptionId { get; set; }
        public virtual PlanSubscription ParentSubscription { get; set; }

        [ForeignKey("ChildUser")]
        public int ChildUserId { get; set; }
        public virtual User ChildUser { get; set; }

        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
