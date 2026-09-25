using AiGateway_Core.Models;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.TextMasking;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AiGateway_Infrastructure.TextMasking
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
