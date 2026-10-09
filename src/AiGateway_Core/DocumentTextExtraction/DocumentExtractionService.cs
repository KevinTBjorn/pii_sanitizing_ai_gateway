using AiGateway_Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.DocumentTextExtraction
{
    public class DocumentExtractionService : IDocumentExtractionService
    {
        private readonly IEnumerable<IFileFormatHandler> _extractors;
        private readonly IFileTypeDetector _fileTypeDetector;

        public DocumentExtractionService(IEnumerable<IFileFormatHandler> extractors, IFileTypeDetector fileTypeDetector)
        {
            _extractors = extractors;
            _fileTypeDetector = fileTypeDetector;
        }

        public async Task<string> ExtractTextAsync(Stream fileStream, string claimedExtension, CancellationToken cancellationToken = default)
        {
            var realExtension = await _fileTypeDetector.DetectTypeAsync(fileStream, claimedExtension, cancellationToken);

            var extractor = _extractors.FirstOrDefault(e => e.CanHandle(realExtension));

            if (extractor == null)
            {
                throw new GatewayException($"No extractor found for '{realExtension}'.", 415);
            }

            string extractedText = await extractor.ExtractTextAsync(fileStream, cancellationToken);

            return DocumentTextNormalizer.NormalizeForLlm(extractedText);
        }
    }
}
