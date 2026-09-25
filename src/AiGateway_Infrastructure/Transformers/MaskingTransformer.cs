using AiGateway_Core.Orchestration;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.TextMasking;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AiGateway_Infrastructure.Transformers
{
    public class MaskingTransformer : IPiiTransformer
    {
        public Task<PiiCheckResultTemp> PiiTransformAsync(string prompt, IReadOnlyList<PiiMatch> matches, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt) || matches == null || matches.Count == 0)
            {
                return Task.FromResult(new PiiCheckResultTemp(
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

            return Task.FromResult(new PiiCheckResultTemp(
                CleanedPrompt: builder.ToString(),
                HasPii: true
            ));
        }
    }
}
