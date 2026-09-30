using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Routing
{
    public record RouteInfo(
        LlmProvider Provider,
        string TargetUrl
    );

    public class LlmRequestContext
    {
        private RouteInfo? _route;
        public RouteInfo Route => _route ?? throw new InvalidOperationException(
            "LlmRequestContext.Route is uninitialized. Ensure LlmRoutingMiddleware ran before accessing Route.");

        public bool IsResolved => _route is not null;

        public void SetRoute(RouteInfo route)
        {
            _route = route ?? throw new ArgumentNullException(nameof(route));
        }
    }
}
