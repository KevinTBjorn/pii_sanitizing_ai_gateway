using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Models
{
    public class PiiGatewayOptions
    {
        public const string SectionName = "PiiOptions";
        public string BaseUrl { get; set; } = "http://127.0.0.1:5001/";
        public int TimeoutSeconds { get; set; } = 13;
        public HashSet<string> IgnoredKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
