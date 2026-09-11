using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Telemetry;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.Coordinators
{
    public class PiiGatewayCoordinator
    {
        private readonly IPiiScanner _scanner;
        private readonly IPiiTransformer _transformer;

        public PiiGatewayCoordinator(IPiiScanner scanner, IPiiTransformer transformer)
        {
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        }

        public async Task<PiiCheckResult> ProcessPromptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new PiiCheckResult(prompt, false);

            var Matches = await _scanner.DetectPiiAsync(prompt, cancellationToken);

            if (Matches.Count == 0)
                return new PiiCheckResult(prompt, false);

            foreach (var match in Matches)
            {
                PiiMetrics.RecordPii(match.EntityType, PiiAction.Masked);
            }

            return await _transformer.PiiTransformAsync(prompt, Matches, cancellationToken);
        }
    }
}
