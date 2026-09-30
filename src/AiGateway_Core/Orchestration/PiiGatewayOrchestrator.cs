using AiGateway_Core.DocumentTextExtraction;
using AiGateway_Core.IncomingLlmPayload;
using AiGateway_Core.LlmPayload;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.Telemetry;
using AiGateway_Core.TextMasking;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AiGateway_Core.Orchestration
{
    public class PiiGatewayOrchestrator : IPiiGatewayOrchestrator
    {
        private readonly IPiiScanner _scanner;
        private readonly IPiiTransformer _transformer;
        private readonly IDocumentExtractionService _extractionService;
        private readonly ILlmPayloadFormatter _llmPayloadFormatter;
        private readonly ILogger<PiiGatewayOrchestrator> _logger;

        public PiiGatewayOrchestrator(IPiiScanner scanner, IPiiTransformer transformer, IDocumentExtractionService extractionService, ILlmPayloadFormatter llmPayloadFormatter, ILogger<PiiGatewayOrchestrator> logger)
        {
            ArgumentNullException.ThrowIfNull(scanner);
            ArgumentNullException.ThrowIfNull(transformer);
            ArgumentNullException.ThrowIfNull(extractionService);
            ArgumentNullException.ThrowIfNull(llmPayloadFormatter);
            ArgumentNullException.ThrowIfNull(logger);

            _scanner = scanner;
            _transformer = transformer;
            _extractionService = extractionService;
            _llmPayloadFormatter = llmPayloadFormatter;
            _logger = logger;
        }

        public async Task<PiiGatewayExecutionResult> OrchestrateSanitizeProcessAsync(RequestPromptContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var entries = new List<TextEntry>();

            if (context.Files is { Count: > 0 })
            {
                foreach (var file in context.Files)
                {
                    string extractedText = await _extractionService.ExtractTextAsync(file.fileStream, file.Extension, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(extractedText))
                    {
                        entries.Add(new TextEntry
                        {
                            Content = extractedText,
                            Source = TextSourceType.Document
                        });
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(context.PromptRequest.Prompt))
            {
                entries.Add(new TextEntry
                {
                    Content = context.PromptRequest.Prompt,
                    Source = TextSourceType.UserPrompt
                });
            }

            IReadOnlyList<TextEntry> scannedEntries = await _scanner.DetectPiiAsync(entries, cancellationToken);

            foreach (var entry in scannedEntries)
            {
                var detectedTypes = entry.Matches.Select(m => m.EntityType).Distinct();

                _logger.LogInformation(
                    "Scan Complete [{Source}]: Found {MatchCount} PII entities. Types: {Types}",
                    entry.Source,
                    entry.Matches.Count,
                    string.Join(", ", detectedTypes));

                // Granular details (visible when LogLevel is Debug)
                foreach (var match in entry.Matches)
                {
                    _logger.LogDebug(
                        "  -> PII Match [{Source}]: Type={EntityType}, start={Start}, length={Length}, Score={Score}, text={DetectedText}",
                        entry.Source, match.EntityType, match.Start, match.Length, match.Score, match.DetectedText);
                }
            }

            bool containsPii = scannedEntries.Any(e => e.Matches.Count > 0);
            var detectedPiiTypes = scannedEntries
                .SelectMany(e => e.Matches)
                .Select(m => m.EntityType)
                .Distinct()
                .ToList();

            IReadOnlyList<TextEntry> sanitizedEntries = scannedEntries;

            if (containsPii)
                sanitizedEntries = await _transformer.PiiTransformAsync(scannedEntries, cancellationToken);

            for (int i = 0; i < scannedEntries.Count; i++)
            {
                var original = scannedEntries[i];
                var sanitized = sanitizedEntries[i];

                _logger.LogInformation(
                    "Transform Complete [{Source}]: Text length changed from {OldLen} to {NewLen} characters.",
                    original.Source, original.Content.Length, sanitized.Content.Length);

                _logger.LogDebug("ORIGINAL [{Source}]:  {Content}", original.Source, original.Content);
                _logger.LogDebug("SANITIZED [{Source}]: {Content}", sanitized.Source, sanitized.Content);
            }

            object formattedPayload = _llmPayloadFormatter.FormatPayload("openai", context.PromptRequest, sanitizedEntries);

            return new PiiGatewayExecutionResult
            {
                FormattedPayload = formattedPayload,
                ContainsPii = containsPii,
                DetectedPiiTypes = detectedPiiTypes,
            };
        }

        //public async void test()
        //{
        //    var cancellationToken = new CancellationToken();

        //    string localFilePath = @"C:\repos\AiGateway\DocumentTest\pdfTest.pdf";
        //    string extension = Path.GetExtension(localFilePath);

        //    try
        //    {
        //        // 5. Open stream and pass to extraction pipeline
        //        await using var fileStream = File.OpenRead(localFilePath);
        //        string extractedText = await _extractionService.ExtractTextAsync(fileStream, extension, cancellationToken);

        //        Console.WriteLine("\n--- Extracted Text Result ---");
        //        Console.WriteLine(extractedText);
        //        Console.WriteLine("----------------------------");

        //        var Matches = await _scanner.DetectPiiAsync(extractedText, cancellationToken);

        //        foreach (var match in Matches)
        //        {
        //            Console.WriteLine($"[PiiGatewayOrchestrator] Detected PII match with no overlap: {match}");
        //            PiiMetrics.RecordPii(match.EntityType, PiiAction.Masked);
        //        }
        //        var cleandprompt = await _transformer.PiiTransformAsync(extractedText, Matches, cancellationToken);
        //        Console.WriteLine($"[PiiGatewayOrchestrator] Cleaned prompt: {cleandprompt.CleanedPrompt}");

        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Extraction failed: {ex.Message}");
        //    }

        //}
    }
}