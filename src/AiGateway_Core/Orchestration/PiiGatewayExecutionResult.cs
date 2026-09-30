using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Orchestration
{
    public record PiiGatewayExecutionResult
    {
        public required object FormattedPayload { get; init; }
        public bool ContainsPii { get; init; }
        public required IReadOnlyList<string> DetectedPiiTypes { get; init; } = Array.Empty<string>();
    }
}
