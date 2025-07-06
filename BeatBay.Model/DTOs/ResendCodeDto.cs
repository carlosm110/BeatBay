using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatBay.Model.DTOs
{
    public class ResendCodeDto
    {
        [Required]
        public string UserName { get; set; }
    }
}