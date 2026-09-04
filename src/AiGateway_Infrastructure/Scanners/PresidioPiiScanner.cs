using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Telemetry;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Lingua;

namespace AiGateway_Infrastructure.Scanners
{
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
            {
                return new PiiScanResult(
                    HasSensitiveData: false,
                    CleanedPrompt: prompt,
                    DetectedTypes: Array.Empty<string>()
                );
            }

            var detectedLanguage = _languageDetector.DetectLanguage(prompt);

            // 1. Send request til Presidio Analyzer i Docker
            var response = await _httpClient.PostAsJsonAsync("/analyze", new
            {
                text = prompt,
                language = detectedLanguage
            }, cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            // 2. Parse fundne enheder
            var entities = doc.RootElement.EnumerateArray()
                .Select(e => new
                {
                    Start = e.GetProperty("start").GetInt32(),
                    End = e.GetProperty("end").GetInt32(),
                    Type = e.GetProperty("entity_type").GetString() ?? "PII"
                })
                .OrderByDescending(e => e.Start) // Bagfra for ikke at ødelægge index
                .ToList();

            if (entities.Count == 0)
            {
                return new PiiScanResult(
                    HasSensitiveData: false,
                    CleanedPrompt: prompt,
                    DetectedTypes: Array.Empty<string>()
                );
            }

            // 3. Masker teksten i C#
            var builder = new StringBuilder(prompt);
            var detectedTypes = new HashSet<string>();

            foreach (var entity in entities)
            {
                var length = entity.End - entity.Start;
                builder.Remove(entity.Start, length);
                builder.Insert(entity.Start, $"[{entity.Type}]");
                detectedTypes.Add(entity.Type);

                // Registrer i Prometheus/OpenTelemetry metrics
                PiiMetrics.RecordMaskedItem($"presidio_{entity.Type.ToLowerInvariant()}", 1);
            }

            // 4. Returner dit PiiScanResult
            return new PiiScanResult
            (
                HasSensitiveData: true,
                CleanedPrompt: builder.ToString(),
                DetectedTypes: detectedTypes.ToList().AsReadOnly()
            );
        }
    }
}
