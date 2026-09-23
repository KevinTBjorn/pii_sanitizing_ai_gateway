using AiGateway_Core.DocumentExtraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.IDocumentTextExtractor
{
    public interface IDocumentExtractionService
    {
        Task<string> ExtractTextAsync(Stream fileStream, string extension, CancellationToken cancellationToken = default);
    }

    public class DocumentExtractionService : IDocumentExtractionService
    {
        private readonly IEnumerable<IDocumentTextExtractor> _extractors;

        public DocumentExtractionService(IEnumerable<IDocumentTextExtractor> extractors)
        {
            _extractors = extractors;
        }

        public async Task<string> ExtractTextAsync(Stream fileStream, string extension, CancellationToken cancellationToken = default)
        {
            var extractor = _extractors.FirstOrDefault(e => e.CanHandle(extension));

            if (extractor == null)
            {
                throw new NotSupportedException($"No extractor found for file extension '{extension}'.");
            }

            string extractedText = await extractor.ExtractTextAsync(fileStream, cancellationToken);

            return DocumentTextNormalizer.NormalizeForLlm(extractedText);
        }
    }
}
