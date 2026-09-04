using AiGateway_Core.Interfaces;
using Lingua;
using static Lingua.Language;

namespace AiGateway_Infrastructure.LanguageDetection
{
    public class LinguaLanguageDetector : ILanguageDetector
    {
        private readonly LanguageDetector _detector;
        private readonly Dictionary<Language, string> _languageToIsoMap;

        public LinguaLanguageDetector()
        : this(new[] { "da", "en" })
        {
        }
        public LinguaLanguageDetector(IEnumerable<string> supportedIsoCodes)
        {
            var isoList = supportedIsoCodes?.ToList() ?? new List<string> { "da", "en" };

            // 1. Map ISO-strenge ("da", "en", "de") til Lingua Language Enums
            var linguaLanguages = new List<Language>();
            _languageToIsoMap = new Dictionary<Language, string>();

            foreach (var iso in isoList)
            {
                if (TryParseIsoCode(iso, out var language))
                {
                    linguaLanguages.Add(language);
                    _languageToIsoMap[language] = iso.ToLowerInvariant();
                }
            }

            // 2. Byg Lingua detector med de valgte sprog
            _detector = LanguageDetectorBuilder
                .FromLanguages(linguaLanguages.ToArray())
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
        private static bool TryParseIsoCode(string isoCode, out Language language)
        {
            language = isoCode.ToLowerInvariant() switch
            {
                "da" => Danish,
                "en" => English,
                _ => Unknown
            };

            return language != Language.Unknown;
        }
    }
}
