using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AiGateway_Core.Options
{
    public class PresidioOptions
    {
        public const string SectionName = "PresidioOptions";
        [Required]
        public Uri PresidioUrl { get; set; } = new Uri("http://127.0.0.1:5001/");
        [Range(1, 60)]
        public int TimeoutSeconds { get; set; } = 13;
    }
}