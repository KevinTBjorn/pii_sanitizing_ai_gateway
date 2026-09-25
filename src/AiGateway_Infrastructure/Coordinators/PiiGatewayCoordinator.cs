using AiGateway_Core.DocumentTextExtraction;
using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.Telemetry;
using AiGateway_Core.TextMasking;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.Coordinators
{
    public class PiiGatewayCoordinator : IPiiGatewayCoordinator
    {
        private readonly IPiiScanner _scanner;
        private readonly IPiiTransformer _transformer;
        private readonly IDocumentExtractionService _extractionService;

        public PiiGatewayCoordinator(IPiiScanner scanner, IPiiTransformer transformer, IDocumentExtractionService extractionService)
        {
            ArgumentNullException.ThrowIfNull(scanner);
            ArgumentNullException.ThrowIfNull(transformer);

            _scanner = scanner;
            _transformer = transformer;
            _extractionService = extractionService;
        }

        public async Task<PiiCheckResult> SanitizeTextAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new PiiCheckResult(prompt, false);

            var Matches = await _scanner.DetectPiiAsync(prompt, cancellationToken);

            if (Matches.Count == 0)
                return new PiiCheckResult(prompt, false);

            foreach (var match in Matches)
            {
                Console.WriteLine($"[PiiGatewayCoordinator] Detected PII match with no overlap: {match}");
                PiiMetrics.RecordPii(match.EntityType, PiiAction.Masked);
            }
            var cleandprompt = await _transformer.PiiTransformAsync(prompt, Matches, cancellationToken);

            Console.WriteLine($"[PiiGatewayCoordinator] Cleaned prompt: {cleandprompt.CleanedPrompt}");

            return cleandprompt;
        }

        public async void test()
        {
            var cancellationToken = new CancellationToken();

            string localFilePath = @"C:\repos\AiGateway\DocumentTest\pdfTest.pdf";
            string extension = Path.GetExtension(localFilePath);

            try
            {
                // 5. Open stream and pass to extraction pipeline
                await using var fileStream = File.OpenRead(localFilePath);
                string extractedText = await _extractionService.ExtractTextAsync(fileStream, extension, cancellationToken);

                Console.WriteLine("\n--- Extracted Text Result ---");
                Console.WriteLine(extractedText);
                Console.WriteLine("----------------------------");

                var Matches = await _scanner.DetectPiiAsync(extractedText, cancellationToken);

                foreach (var match in Matches)
                {
                    Console.WriteLine($"[PiiGatewayCoordinator] Detected PII match with no overlap: {match}");
                    PiiMetrics.RecordPii(match.EntityType, PiiAction.Masked);
                }
                var cleandprompt = await _transformer.PiiTransformAsync(extractedText, Matches, cancellationToken);
                Console.WriteLine($"[PiiGatewayCoordinator] Cleaned prompt: {cleandprompt.CleanedPrompt}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Extraction failed: {ex.Message}");
            }

        }
    }
}