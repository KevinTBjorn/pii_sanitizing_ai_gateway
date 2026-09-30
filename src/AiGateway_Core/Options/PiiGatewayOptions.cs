using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AiGateway_Core.Options
{
    public class PiiGatewayOptions
    {
        public const string SectionName = "PiiOptions";
        [Required, Url]
        public string presidioURL { get; set; } = "http://127.0.0.1:5001/";
        [Range(1, 60)]
        public int TimeoutSeconds { get; set; } = 13;
    }
}