using AiGateway_Core.RequestParser;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Routing
{
    public interface IGatewayRouter
    {
        RouteDestination? ResolveRoute(string? modelName);
    }
}
