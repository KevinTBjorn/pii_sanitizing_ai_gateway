using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.IncomingLlmPayload
{
    public record RequestPromptPayload
    {
        public required string Prompt { get; init; }
        public string? Model { get; init; }
        public double Temperature { get; init; } = 0.7;
    }
}
