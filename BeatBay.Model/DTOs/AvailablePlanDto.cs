using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatBay.DTOs
{
    public class AvailablePlanDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal PriceUSD { get; set; }
        public int MaxConnections { get; set; }
    }
}
