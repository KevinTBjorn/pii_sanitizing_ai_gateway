using AiGateway_Core.Orchestration;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.Telemetry;
using AiGateway_Core.TextMasking;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AiGateway_Infrastructure.TextMasking
{
    public class MaskingTransformer : IPiiTransformer
    {
        public Task<IReadOnlyList<TextEntry>> PiiTransformAsync(IReadOnlyList<TextEntry> entries, CancellationToken cancellationToken = default)
        {
            var sanitizedEntries = new List<TextEntry>(entries.Count);

            foreach (var entry in entries)
            {
                if (entry.Matches == null || entry.Matches.Count == 0)
                {
                    sanitizedEntries.Add(entry);
                    continue;
                }

                foreach (var match in entry.Matches)
                {
                    PiiMetrics.RecordPii(match.EntityType, PiiAction.Masked);
                }

                string sanitizedContent = MaskContent(entry.Content, entry.Matches);

                sanitizedEntries.Add(entry with
                {
                    Content = sanitizedContent,
                    Matches = Array.Empty<PiiMatch>()
                });
            }

            return Task.FromResult<IReadOnlyList<TextEntry>>(sanitizedEntries);
        }

        private static string MaskContent(string prompt, IReadOnlyList<PiiMatch> matches)
        {
            if (string.IsNullOrEmpty(prompt) || matches == null || matches.Count == 0)
            {
                return prompt;
            }

            var builder = new StringBuilder(prompt);

            foreach (var match in matches.OrderByDescending(m => m.Start))
            {
                string replacementText = $"[MASKED_{match.EntityType.ToUpperInvariant()}]";

                builder.Remove(match.Start, match.Length);
                builder.Insert(match.Start, replacementText);
            }

            return builder.ToString();
        }
    }
}
