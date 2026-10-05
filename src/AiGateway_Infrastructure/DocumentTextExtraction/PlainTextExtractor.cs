using AiGateway_Core.DocumentTextExtraction;
using AiGateway_Infrastructure.DocumentTextExtraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.DocumentTextExtraction
{
    public class PlainTextExtractor : IFileFormatHandler
    {
        public bool CanHandle(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            return SupportedTextExtensions.Extensions.Contains(extension.StartsWith(".") ? extension : "." + extension);
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
