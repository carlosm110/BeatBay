using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatBay.DTOs
{
    public class CurrentSubscriptionDto
    {
        public int Id { get; set; }
        public string PlanName { get; set; }
        public decimal PriceUSD { get; set; }
        public int MaxConnections { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public List<UserConnectionDto> Connections { get; set; } = new List<UserConnectionDto>();
    }
}
