using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AiGateway_Core.LanguageDetection
{
    public class LanguageDetectionOptions
    {
        public const string SectionName = "LanguageDetection";

        [Required]
        [MinLength(1, ErrorMessage = "LanguageDetection:SupportedLanguages cannot be null or empty.")]
        public List<string> SupportedLanguages { get; set; } = new() { "da", "en" };

        [Required]
        public string DefaultFallback { get; set; } = "en";
    }
}
