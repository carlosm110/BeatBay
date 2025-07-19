using BeatBay.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatBay.DTOs
{
    public class PlanSubscriptionDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public int PlanId { get; set; }
        public string PlanName { get; set; }
        public decimal PriceUSD { get; set; }
        public int MaxConnections { get; set; }
        public int UsedConnections { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public List<UserDto> ConnectedUsers { get; set; } = new List<UserDto>();
    }
}
