using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Telemetry;
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
            Console.WriteLine($"[PiiGatewayCoordinator] Processing prompt: {prompt}");

            var Matches = await _scanner.DetectPiiAsync(prompt, cancellationToken);

            foreach (var match in Matches)
            {
                Console.WriteLine($"[PiiGatewayCoordinator] Detected PII match with no overlap: {match}");
            }

            if (Matches.Count == 0)
                return new PiiCheckResult(prompt, false);

            return await _transformer.PiiTransformAsync(prompt, Matches, cancellationToken);
        }
    }
}
