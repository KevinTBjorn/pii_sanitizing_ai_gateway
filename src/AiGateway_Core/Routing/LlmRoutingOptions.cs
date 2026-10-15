using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Routing
{
    public class LlmRoutingOptions
    {
        public const string SectionName = "LlmRoutingOptions";

        public List<RouteConfig> ModelRoutes { get; set; } = new();
    }
}
