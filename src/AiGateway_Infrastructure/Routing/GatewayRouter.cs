using AiGateway_Core.Exceptions;
using AiGateway_Core.RequestParser;
using AiGateway_Core.Routing;
using Microsoft.Extensions.Options;

namespace AiGateway_Infrastructure.Routing
{
    public class GatewayRouter : IGatewayRouter
    {
        private readonly Dictionary<string, RouteConfig> _routeLookup;

        public GatewayRouter(IOptions<LlmRoutingOptions> options)
        {
            var opts = options.Value ?? throw new ArgumentNullException(nameof(options));

            _routeLookup = opts.ModelRoutes.ToDictionary(
                route => route.Model,
                route => route,
                StringComparer.OrdinalIgnoreCase);
        }

        public RouteDestination? ResolveRoute(string? modelName)
        {
            if (!string.IsNullOrWhiteSpace(modelName) && _routeLookup.TryGetValue(modelName, out var route))
            {
                return new RouteDestination(route.Url, route.Format);
            }

            return null;
        }
    }
}