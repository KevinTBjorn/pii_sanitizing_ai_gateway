using AiGateway_Core.PiiScanners;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Orchestration
{
    public enum TextSourceType
    {
        UserPrompt,
        Document
    }
    public record TextEntry
    {
        public required string Content { get; init; }
        public required TextSourceType Source { get; init; }
        public IReadOnlyList<PiiMatch> Matches { get; init; } = Array.Empty<PiiMatch>();
    }
}
