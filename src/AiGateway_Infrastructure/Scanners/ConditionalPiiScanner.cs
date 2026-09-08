using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.Scanners
{
    public class ConditionalPiiScanner : IPiiScanner
    {
        private readonly IEnumerable<IPiiScanner> _scanners;

        public ConditionalPiiScanner(IEnumerable<IPiiScanner> scanners)
        {
            _scanners = scanners;
        }

        public async Task<PiiScanResult> ScanAndMaskAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new PiiScanResult(false, prompt, Array.Empty<string>());
            }

            var currentPrompt = prompt;
            var allDetectedTypes = new HashSet<string>();
            bool hasAnySensitiveData = false;

            // Kør hver scanner i rækkefølge (f.eks. 1. RegexPiiScanner -> 2. PresidioPiiScanner)
            foreach (var scanner in _scanners)
            {
                var result = await scanner.ScanAndMaskAsync(currentPrompt, cancellationToken);

                if (result.HasSensitiveData)
                {
                    hasAnySensitiveData = true;
                    currentPrompt = result.CleanedPrompt; // Send den maskerede tekst videre til næste scanner

                    foreach (var type in result.DetectedTypes)
                    {
                        allDetectedTypes.Add(type);
                    }
                }
            }

            return new PiiScanResult(
                HasSensitiveData: hasAnySensitiveData,
                CleanedPrompt: currentPrompt,
                DetectedTypes: allDetectedTypes.ToList().AsReadOnly()
            );
        }
    }
}
