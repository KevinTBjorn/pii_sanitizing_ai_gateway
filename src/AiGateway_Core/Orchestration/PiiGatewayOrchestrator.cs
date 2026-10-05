using AiGateway_Core.DocumentTextExtraction;
using AiGateway_Core.LlmPayload;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.RequestParser;
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

            bool containsPii = scannedEntries.Any(e => e.Matches.Count > 0);
            var detectedPiiTypes = scannedEntries
                .SelectMany(e => e.Matches)
                .Select(m => m.EntityType)
                .Distinct()
                .ToList();

            IReadOnlyList<TextEntry> sanitizedEntries = scannedEntries;

            if (containsPii)
            {
                sanitizedEntries = await _transformer.PiiTransformAsync(scannedEntries, cancellationToken);
            }

            PrintSanitizationShowcase(scannedEntries, sanitizedEntries);

            object formattedPayload = _llmPayloadFormatter.FormatPayload("openai", context.PromptRequest, sanitizedEntries);

            return new PiiGatewayExecutionResult
            {
                FormattedPayload = formattedPayload,
                ContainsPii = containsPii,
                DetectedPiiTypes = detectedPiiTypes,
            };
        }

        private static void PrintSanitizationShowcase(IReadOnlyList<TextEntry> scannedEntries, IReadOnlyList<TextEntry> sanitizedEntries)
        {
            const string Reset = "\u001b[0m";
            const string Red = "\u001b[31m";
            const string Green = "\u001b[32m";
            const string Yellow = "\u001b[33m";
            const string Cyan = "\u001b[36m";
            const string Gray = "\u001b[90m";

            Console.WriteLine($"{Cyan}\n=== PII GATEWAY SHOWCASE ==={Reset}");

            foreach (var entry in scannedEntries)
            {
                var detectedTypes = entry.Matches.Select(m => m.EntityType).Distinct();
                string typesText = detectedTypes.Any() ? string.Join(", ", detectedTypes) : "None";
                string color = entry.Matches.Count > 0 ? Yellow : Green;

                Console.WriteLine($"{color}[SCAN] {entry.Source} | Matches: {entry.Matches.Count} | Types: {typesText}{Reset}");

                foreach (var match in entry.Matches)
                {
                    int endIndex = match.Start + match.Length;
                    Console.WriteLine($"  └─> {Cyan}[{match.EntityType}]{Reset} Pos: {match.Start}..{endIndex} ({match.Length} chars) | Score: {match.Score:F2} | Text: \"{match.DetectedText}\"");
                }
            }

            Console.WriteLine($"{Gray}--------------------------------------------------{Reset}");

            for (int i = 0; i < scannedEntries.Count; i++)
            {
                var original = scannedEntries[i];
                var sanitized = sanitizedEntries[i];

                Console.WriteLine($"[TRANSFORM] {original.Source} | Length: {original.Content.Length} -> {sanitized.Content.Length} chars");
                Console.WriteLine($"{Red}ORIGINAL : {original.Content}{Reset}");
                Console.WriteLine($"{Green}SANITIZED: {sanitized.Content}{Reset}");
            }

            Console.WriteLine($"{Cyan}=========================================\n{Reset}");

            Console.Out.Flush();
        }
    }
}