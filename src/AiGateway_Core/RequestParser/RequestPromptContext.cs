using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.RequestParser
{
    public record RequestPromptContext
    {
        public required RequestPromptPayload PromptRequest { get; init; }
        public IReadOnlyList<InputFile> Files { get; init; } = Array.Empty<InputFile>();

        public async ValueTask DisposeAsync()
        {
            foreach (var file in Files)
            {
                await file.fileStream.DisposeAsync();
            }
        }
    }

    public record InputFile(Stream fileStream, string Extension);
}
