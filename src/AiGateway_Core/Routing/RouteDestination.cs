using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Routing
{
    public record RouteDestination(Uri Endpoint, LlmPayloadFormat Format);
}
