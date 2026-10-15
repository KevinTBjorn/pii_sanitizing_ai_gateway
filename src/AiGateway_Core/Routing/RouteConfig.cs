using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Routing
{
    public class RouteConfig
    {
        public required string Model { get; set; }
        public required Uri Url { get; set; }
        public LlmPayloadFormat Format { get; set; } = LlmPayloadFormat.OpenAi;
    }
}
