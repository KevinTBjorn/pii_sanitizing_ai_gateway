using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.RequestParser
{
    public record RequestPromptPayload
    {
        public required string Model { get; init; }
        public IReadOnlyList<ChatMessagePayload> Messages { get; init; } = Array.Empty<ChatMessagePayload>();
        public double? Temperature { get; init; }
        public double? TopP { get; init; }
        public int? MaxTokens { get; init; }
    }
}
