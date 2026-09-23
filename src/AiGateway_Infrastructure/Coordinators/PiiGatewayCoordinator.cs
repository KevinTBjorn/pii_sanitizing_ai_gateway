using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Telemetry;
using AiGateway_Infrastructure.TextExtraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.Coordinators
{
    public class PiiGatewayCoordinator : IPiiGatewayCoordinator
    {
        private readonly IPiiScanner _scanner;
        private readonly IPiiTransformer _transformer;

        public PiiGatewayCoordinator(IPiiScanner scanner, IPiiTransformer transformer)
        {
            ArgumentNullException.ThrowIfNull(scanner);
            ArgumentNullException.ThrowIfNull(transformer);

            _scanner = scanner;
            _transformer = transformer;
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
    }
}
