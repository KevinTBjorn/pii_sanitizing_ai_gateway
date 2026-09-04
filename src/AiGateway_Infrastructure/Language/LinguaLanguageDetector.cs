using AiGateway_Core.Interfaces;
using Lingua;
using static Lingua.Language;

namespace AiGateway_Infrastructure.Language
{
    public class LinguaLanguageDetector : ILanguageDetector
    {
        private readonly LanguageDetector _detector;

        public LinguaLanguageDetector()
        {
            _detector = LanguageDetectorBuilder
                .FromLanguages(Danish, English)
                .WithPreloadedLanguageModels()
                .Build();
        }

        public string DetectLanguage(string text)
        => DetectLanguage(text, fallback: "en");

        public string DetectLanguage(string text, string fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;

            var detectedLanguage = _detector.DetectLanguageOf(text);

            return detectedLanguage switch
            {
                Danish => "da",
                English => "en",
                _ => fallback
            };
        }
    }
}
