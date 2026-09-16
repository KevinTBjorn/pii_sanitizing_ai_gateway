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
        public string BaseUrl { get; set; } = "http://127.0.0.1:5001/";
        [Range(1, 60)]
        public int TimeoutSeconds { get; set; } = 13;
        [Required, MinLength(1, ErrorMessage = "IgnoredKeys must contain at least one key.")]
        public HashSet<string> IgnoredKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
