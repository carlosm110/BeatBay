using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatBay.Model.DTOs
{
    public class Enable2FADto
    {
        [Required]
        public string Password { get; set; }
    }
}