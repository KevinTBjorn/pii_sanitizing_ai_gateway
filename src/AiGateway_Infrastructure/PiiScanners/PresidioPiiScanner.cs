using AiGateway_Core.LanguageDetection;
using AiGateway_Core.Orchestration;
using AiGateway_Core.PiiScanners;
using Lingua;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AiGateway_Infrastructure.PiiScanners
{
    public class PresidioPiiScanner : IPiiScanner
    {
        private record PresidioResponseDto(
            [property: JsonPropertyName("entity_type")] string EntityType,
            [property: JsonPropertyName("start")] int Start,
            [property: JsonPropertyName("end")] int End,
            [property: JsonPropertyName("score")] double Score
        );
        private readonly HttpClient _httpClient;
        private readonly ILanguageDetector _languageDetector;

        public PresidioPiiScanner(HttpClient httpClient, ILanguageDetector languageDetector)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(languageDetector);

            _httpClient = httpClient;
            _languageDetector = languageDetector;
        }

        public async Task<IReadOnlyList<TextEntry>> DetectPiiAsync(IReadOnlyList<TextEntry> entries, CancellationToken cancellationToken = default)
        {
            if (entries is null || entries.Count == 0)
            {
                return Array.Empty<TextEntry>();
            }

            var scanTasks = entries.Select(entry => ScanSingleEntryAsync(entry, cancellationToken));

            TextEntry[] processedEntries = await Task.WhenAll(scanTasks);

            return processedEntries;
        }

        private async Task<TextEntry> ScanSingleEntryAsync(TextEntry entry, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entry.Content))
            {
                return entry;
            }

            var request = new
            {
                text = entry.Content,
                language = _languageDetector.DetectLanguage(entry.Content)
            };

            var response = await _httpClient.PostAsJsonAsync("analyze", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var presidioResults = await response.Content.ReadFromJsonAsync<List<PresidioResponseDto>>(cancellationToken) ?? new();

            var rawMatches = new List<PiiMatch>();

            foreach (var r in presidioResults)
            {
                int start = CodePointToUtf16Offset(entry.Content, r.Start);
                int end = CodePointToUtf16Offset(entry.Content, r.End);
                int length = end - start;

                if (start < 0 || start >= entry.Content.Length || length <= 0)
                {
                    continue;
                }

                int safeLength = Math.Min(length, entry.Content.Length - start);

                rawMatches.Add(new PiiMatch(
                    Start: start,
                    Length: safeLength,
                    EntityType: r.EntityType,
                    Score: r.Score,
                    DetectedText: entry.Content.Substring(start, safeLength)
                ));
            }

            return entry with { Matches = ResolveOverlaps(rawMatches) };
        }

        private static List<PiiMatch> ResolveOverlaps(IEnumerable<PiiMatch> matches)
        {
            var orderedMatches = matches
                .OrderByDescending(m => m.Score == 1.0)
                .ThenByDescending(m => m.Length)
                .ThenByDescending(m => m.Score)
                .ThenBy(m => m.Start)
                .ToList();

            var accepted = new List<PiiMatch>();

            foreach (var candidate in orderedMatches)
            {
                int candidateEnd = candidate.Start + candidate.Length;

                bool hasOverlap = accepted.Any(a =>
                {
                    int acceptedEnd = a.Start + a.Length;
                    return candidate.Start < acceptedEnd && candidateEnd > a.Start;
                });

                if (!hasOverlap)
                {
                    accepted.Add(candidate);
                }
            }

            return accepted.OrderBy(m => m.Start).ToList();
        }

        private static int CodePointToUtf16Offset(string text, int codePointIndex)
        {
            if (string.IsNullOrEmpty(text) || codePointIndex <= 0) return 0;

            int utf16Index = 0;
            int currentCodePoint = 0;

            while (utf16Index < text.Length && currentCodePoint < codePointIndex)
            {
                if (char.IsHighSurrogate(text[utf16Index]) && utf16Index + 1 < text.Length && char.IsLowSurrogate(text[utf16Index + 1]))
                {
                    utf16Index += 2;
                }
                else
                {
                    utf16Index += 1;
                }

                currentCodePoint++;
            }

            return utf16Index;
        }
    }
}
