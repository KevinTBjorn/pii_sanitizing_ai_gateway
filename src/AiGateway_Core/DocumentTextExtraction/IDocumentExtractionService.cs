using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.DocumentTextExtraction
{
    public interface IDocumentExtractionService
    {
        Task<string> ExtractTextAsync(Stream fileStream, string extension, CancellationToken cancellationToken = default);
    }
}
