using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Telemetry;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AiGateway_Infrastructure.Transformers
{
    public class MaskingTransformer : IPiiTransformer
    {
        public Task<PiiCheckResult> PiiTransformAsync(string prompt, IReadOnlyList<PiiMatch> matches, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt) || matches == null || matches.Count == 0)
            {
                return Task.FromResult(new PiiCheckResult(
                    CleanedPrompt: prompt,
                    HasPii: false
                ));
            }

            foreach (var match in matches)
            {
                PiiMetrics.RecordPii(match.EntityType, PiiAction.Masked);
            }

            var builder = new StringBuilder(prompt);

            foreach (var match in matches.OrderByDescending(m => m.Start))
            {
                string replacementText = $"[MASKED_{match.EntityType.ToUpperInvariant()}]";

                builder.Remove(match.Start, match.Length);
                builder.Insert(match.Start, replacementText);
            }

            return Task.FromResult(new PiiCheckResult(
                CleanedPrompt: builder.ToString(),
                HasPii: true
            ));
        }
    }
}
