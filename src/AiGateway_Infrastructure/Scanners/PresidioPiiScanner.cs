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
    public class PresidioEntity
    {
        [JsonPropertyName("start")]
        public int Start { get; set; }

        [JsonPropertyName("end")]
        public int End { get; set; }

        [JsonPropertyName("entity_type")]
        public string EntityType { get; set; } = string.Empty;
    }

    public class PresidioPiiScanner : IPiiScanner
    {
        private readonly HttpClient _httpClient;
        private readonly ILanguageDetector _languageDetector;

        public PresidioPiiScanner(HttpClient httpClient, ILanguageDetector languageDetector)
        {
            _httpClient = httpClient;
            _languageDetector = languageDetector;
        }

        public async Task<PiiScanResult> ScanAndMaskAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return PiiScanResult.NoPiiFound(prompt);

            var rawEntities = await FetchPresidioEntitiesAsync(prompt, cancellationToken);

            if (rawEntities == null || rawEntities.Count == 0)
                return PiiScanResult.NoPiiFound(prompt);

            var existingMaskRanges = FindExistingMaskRanges(prompt);
            var validEntities = FilterExistingMaskOverlaps(rawEntities, existingMaskRanges);

            if (validEntities == null || validEntities.Count == 0)
                return PiiScanResult.NoPiiFound(prompt);

            var finalEntities = RemovePresidioMaskOverlap(validEntities);
            var (maskedPrompt, detectedTypes) = ApplyMasking(prompt, finalEntities);

            return new PiiScanResult
            (
                HasSensitiveData: true,
                CleanedPrompt: maskedPrompt,
                DetectedTypes: detectedTypes.AsReadOnly()
            );

        }

        private static (string maskedPrompt, List<string> detectedTypes) ApplyMasking(string prompt, List<PresidioEntity> Entities)
        {
            var replaceOrder = Entities.OrderByDescending(e => e.Start).ToList();

            var builder = new StringBuilder(prompt);
            var detectedTypes = new HashSet<string>();
            Console.WriteLine("before masking: " + builder.ToString());
            foreach (var entity in replaceOrder)
            {
                int length = entity.End - entity.Start;
                if (entity.Start >= 0 && entity.Start + length <= builder.Length)
                {
                    var test = prompt.Substring(entity.Start, length);
                    if (!PiiTokens.All.Contains(test))
                    {
                        builder.Remove(entity.Start, length);
                        builder.Insert(entity.Start, $"[{entity.EntityType}]");

                        detectedTypes.Add(entity.EntityType);

                        PiiMetrics.RecordMaskedItem($"presidio_{entity.EntityType.ToLowerInvariant()}", 1);
                    }
                }
            }
            Console.WriteLine("after masking: " + builder.ToString());
            return (builder.ToString(), detectedTypes.ToList());
        }

        private static List<(int Start, int End)> FindExistingMaskRanges(string text)
        {
            var existingMaskRanges = new List<(int Start, int End)>();

            foreach (var token in PiiTokens.All)
            {
                int index = text.IndexOf(token, StringComparison.Ordinal);
                while (index != -1)
                {
                    existingMaskRanges.Add((index, index + token.Length));
                    index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal);
                }
            }
            return existingMaskRanges;
        }

        private async Task<List<PresidioEntity>> FetchPresidioEntitiesAsync(string prompt, CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                text = prompt,
                language = _languageDetector.DetectLanguage(prompt),
                allow_list = PiiTokens.All
            };
            var response = await _httpClient.PostAsJsonAsync("analyze", requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();
            var rawEntities = await response.Content.ReadFromJsonAsync<List<PresidioEntity>>(cancellationToken);
            return rawEntities ?? new List<PresidioEntity>();
        }

        private static List<PresidioEntity> FilterExistingMaskOverlaps(List<PresidioEntity> rawEntities, List<(int Start, int End)> existingMaskRanges)
        {
            return rawEntities.Where(entity =>
            {
                // Tjek om Presidios interval (Start -> End) overlapper med et eksisterende masket interval
                bool overlapsWithMaskedToken = existingMaskRanges.Any(range =>
                    entity.Start < range.End && entity.End > range.Start
                );

                return !overlapsWithMaskedToken; // Behold kun entiteter der IKKE overlapper
            }).ToList();
        }

        private static List<PresidioEntity> RemovePresidioMaskOverlap(List<PresidioEntity> Entities)
        {
            var acceptedEntities = new List<PresidioEntity>();
            var longestFirst = Entities.OrderByDescending(e => e.End - e.Start).ToList();

            foreach (var entity in longestFirst)
            {
                bool overlaps = acceptedEntities.Any(accepted =>
                    Math.Max(entity.Start, accepted.Start) < Math.Min(entity.End, accepted.End)
                );

                if (!overlaps)
                {
                    acceptedEntities.Add(entity);
                }
            }

            return acceptedEntities;
        }
    }
}
