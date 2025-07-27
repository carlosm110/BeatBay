using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatBay.DTOs
{
    public class PurchasePlanResponseDto
    {
        public string PaymentId { get; set; }
        public int LocalPaymentId { get; set; }
        public string ApprovalUrl { get; set; }
    }
}