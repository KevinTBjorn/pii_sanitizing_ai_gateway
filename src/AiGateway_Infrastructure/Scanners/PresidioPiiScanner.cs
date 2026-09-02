using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;

namespace AiGateway_Infrastructure.Scanners
{
    public class PresidioPiiScanner : IPiiScanner
    {
        private readonly HttpClient _httpClient;

        public PresidioPiiScanner(HttpClient httpClient)
        {
            _httpClient = httpClient;
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

            // 1. Send request til Presidio Analyzer i Docker
            var response = await _httpClient.PostAsJsonAsync("/analyze", new
            {
                text = prompt,
                language = "en"
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

            // 3. Masker teksten i C#
            var builder = new StringBuilder(prompt);
            var detectedTypes = new List<string>();

            foreach (var entity in entities)
            {
                var length = entity.End - entity.Start;
                builder.Remove(entity.Start, length);
                builder.Insert(entity.Start, $"[{entity.Type}]");
                detectedTypes.Add(entity.Type);
            }

            // 4. Returner dit PiiScanResult
            var result = new PiiScanResult
            (
                HasSensitiveData: entities.Count > 0,
                CleanedPrompt: builder.ToString(),
                DetectedTypes: detectedTypes
            );

            return result;
        }
    }
}
