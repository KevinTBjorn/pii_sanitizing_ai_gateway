using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.IncomingLlmPayload
{
    public record IncomingPromptContext
    {
        public required IncomingPromptPayload PromptRequest { get; init; }
        public IReadOnlyList<InputFile> Files { get; init; } = Array.Empty<InputFile>();
    }

    public record InputFile(Stream fileStream, string Extension);
}
