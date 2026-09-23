using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Interfaces
{
    public interface IDocumentTextExtractor
    {
        bool CanHandle(string extension);
        Task<string> ExtractTextAsync(Stream fileStream, CancellationToken cancellationToken = default);
    }
}
