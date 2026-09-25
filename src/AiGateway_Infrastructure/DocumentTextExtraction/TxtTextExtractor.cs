using AiGateway_Core.DocumentTextExtraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.DocumentTextExtraction
{
    public class TxtTextExtractor : IDocumentTextExtractor
    {
        public bool CanHandle(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            return string.Equals(extension.StartsWith('.') ? extension : $".{extension}", ".txt", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string> ExtractTextAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fileStream);

            // Reset stream pointer if seekable and previously read
            if (fileStream.CanSeek && fileStream.Position != 0)
            {
                fileStream.Position = 0;
            }

            // Leave stream open to respect caller stream ownership
            using var reader = new StreamReader(
                fileStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true);

            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
