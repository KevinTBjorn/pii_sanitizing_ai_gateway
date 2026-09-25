using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Orchestration
{
    public record PiiCheckResult
    {
        public required byte[] SanitizedPayloadBytes { get; init; }
        public bool ContainsPii { get; init; }
        public string AnonymizedPrompt { get; init; } = string.Empty;
        public IReadOnlyList<string> DetectedPiiTypes { get; init; } = Array.Empty<string>();
    }
}
