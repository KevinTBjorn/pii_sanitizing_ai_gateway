using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Forwarding
{
    public class LlmForwardingFeature
    {
        public required Uri TargetEndpoint { get; init; }
        public required object FormattedPayload { get; init; }
    }
}
