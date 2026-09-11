using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Models
{
    public class PiiGatewayOptions
    {
        public const string SectionName = "PiiOptions";
        public HashSet<string> ScannableKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
