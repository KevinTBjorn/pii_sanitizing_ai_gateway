using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.RequestParser
{
    public record RequestPromptPayload
    {
        public required string Prompt { get; init; }
        public required string Model { get; init; }
        public double? Temperature { get; init; }
        public int? Max_Tokens { get; init; }
    }
}
