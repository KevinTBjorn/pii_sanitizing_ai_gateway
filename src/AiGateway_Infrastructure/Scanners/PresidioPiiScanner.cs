using AiGateway_Core.Constants;
using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Telemetry;
using Lingua;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiGateway_Infrastructure.Scanners
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
            _httpClient = httpClient;
            _languageDetector = languageDetector;
        }

        public async Task<IReadOnlyList<PiiMatch>> DetectPiiAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return Array.Empty<PiiMatch>();
            var test = _languageDetector.DetectLanguage(prompt);
            Console.WriteLine($"Detected language: {test}");
            var request = new
            {
                text = prompt,
                language = "da"
            };

            var response = await _httpClient.PostAsJsonAsync("analyze", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var presidioResults = await response.Content.ReadFromJsonAsync<List<PresidioResponseDto>>(cancellationToken) ?? new();

            var rawMatches = presidioResults.Select(r => new PiiMatch(
                    Start: r.Start,
                    Length: r.End - r.Start,
                    EntityType: r.EntityType,
                    Score: r.Score,
                    RawValue: prompt.Substring(r.Start, r.End - r.Start)
                )).ToList();

            return ResolveOverlaps(rawMatches);
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
    }
}
