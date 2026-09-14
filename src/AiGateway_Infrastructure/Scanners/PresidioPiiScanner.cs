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
                language = test
            };

            var response = await _httpClient.PostAsJsonAsync("analyze", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var presidioResults = await response.Content.ReadFromJsonAsync<List<PresidioResponseDto>>(cancellationToken) ?? new();

            return presidioResults.Select(r => new PiiMatch(
                    Start: r.Start,
                    Length: r.End - r.Start,
                    EntityType: r.EntityType,
                    Score: r.Score,
                    RawValue: prompt.Substring(r.Start, r.End - r.Start)
                )).ToList();
        }

        //private static (string maskedPrompt, List<string> detectedTypes) ApplyMasking(string prompt, List<PresidioEntity> Entities)
        //{
        //    var replaceOrder = Entities.OrderByDescending(e => e.Start).ToList();

        //    var builder = new StringBuilder(prompt);
        //    var detectedTypes = new HashSet<string>();
        //    Console.WriteLine("before masking: " + builder.ToString());
        //    foreach (var entity in replaceOrder)
        //    {
        //        int length = entity.End - entity.Start;
        //        if (entity.Start >= 0 && entity.Start + length <= builder.Length)
        //        {
        //            var test = prompt.Substring(entity.Start, length);
        //            if (!PiiTokens.All.Contains(test))
        //            {
        //                builder.Remove(entity.Start, length);
        //                builder.Insert(entity.Start, $"[{entity.EntityType}]");

        //                detectedTypes.Add(entity.EntityType);

        //                PiiMetrics.RecordPii($"presidio_{entity.EntityType.ToLowerInvariant()}", 1);
        //            }
        //        }
        //    }
        //    Console.WriteLine("after masking: " + builder.ToString());
        //    return (builder.ToString(), detectedTypes.ToList());
        //}

        //private static List<(int Start, int End)> FindExistingMaskRanges(string text)
        //{
        //    var existingMaskRanges = new List<(int Start, int End)>();

        //    foreach (var token in PiiTokens.All)
        //    {
        //        int index = text.IndexOf(token, StringComparison.Ordinal);
        //        while (index != -1)
        //        {
        //            existingMaskRanges.Add((index, index + token.Length));
        //            index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal);
        //        }
        //    }
        //    return existingMaskRanges;
        //}

        //private async Task<List<PresidioEntity>> FetchPresidioEntitiesAsync(string prompt, CancellationToken cancellationToken)
        //{
        //    var requestBody = new
        //    {
        //        text = prompt,
        //        language = _languageDetector.DetectLanguage(prompt),
        //        allow_list = PiiTokens.All
        //    };
        //    var response = await _httpClient.PostAsJsonAsync("analyze", requestBody, cancellationToken);
        //    response.EnsureSuccessStatusCode();
        //    var rawEntities = await response.Content.ReadFromJsonAsync<List<PresidioEntity>>(cancellationToken);
        //    return rawEntities ?? new List<PresidioEntity>();
        //}

        //private static List<PresidioEntity> FilterExistingMaskOverlaps(List<PresidioEntity> rawEntities, List<(int Start, int End)> existingMaskRanges)
        //{
        //    return rawEntities.Where(entity =>
        //    {
        //        // Tjek om Presidios interval (Start -> End) overlapper med et eksisterende masket interval
        //        bool overlapsWithMaskedToken = existingMaskRanges.Any(range =>
        //            entity.Start < range.End && entity.End > range.Start
        //        );

        //        return !overlapsWithMaskedToken; // Behold kun entiteter der IKKE overlapper
        //    }).ToList();
        //}

        //private static List<PresidioEntity> RemovePresidioMaskOverlap(List<PresidioEntity> Entities)
        //{
        //    var acceptedEntities = new List<PresidioEntity>();
        //    var longestFirst = Entities.OrderByDescending(e => e.End - e.Start).ToList();

        //    foreach (var entity in longestFirst)
        //    {
        //        bool overlaps = acceptedEntities.Any(accepted =>
        //            Math.Max(entity.Start, accepted.Start) < Math.Min(entity.End, accepted.End)
        //        );

        //        if (!overlaps)
        //        {
        //            acceptedEntities.Add(entity);
        //        }
        //    }

        //    return acceptedEntities;
        //}
    }
}
