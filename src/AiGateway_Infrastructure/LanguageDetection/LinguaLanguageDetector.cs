using AiGateway_Core.LanguageDetection;
using AiGateway_Core.Options;
using Lingua;
using Microsoft.Extensions.Options;
using static Lingua.Language;

namespace AiGateway_Infrastructure.LanguageDetection
{
    public class LinguaLanguageDetector : ILanguageDetector
    {
        private readonly LanguageDetector _detector;
        private readonly Dictionary<Language, string> _languageToIsoMap;
        private readonly string _defaultFallback;

        public LinguaLanguageDetector(IOptions<LanguageDetectionOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.Value);

            var opts = options.Value;

            _defaultFallback = !string.IsNullOrWhiteSpace(opts.DefaultFallback)
                ? opts.DefaultFallback.ToLowerInvariant()
                : "en";

            _languageToIsoMap = new Dictionary<Language, string>();
            var linguaLanguages = new List<Language>();

            foreach (var iso in opts.SupportedLanguages)
            {
                if (TryParseIsoCode(iso, out var language))
                {
                    linguaLanguages.Add(language);
                    _languageToIsoMap[language] = iso.ToLowerInvariant();
                }
            }

            if (linguaLanguages.Count == 0)
            {
                throw new InvalidOperationException(
                    "None of the configured SupportedLanguages could be mapped to valid Lingua Language enums.");
            }

            _detector = LanguageDetectorBuilder
                .FromLanguages(linguaLanguages.ToArray())
                .WithPreloadedLanguageModels()
                .Build();
        }

        public string DetectLanguage(string text)
                    => DetectLanguage(text, fallback: _defaultFallback);

        public string DetectLanguage(string text, string fallback)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            var detectedLanguage = _detector.DetectLanguageOf(text);

            return _languageToIsoMap.TryGetValue(detectedLanguage, out var isoCode)
                ? isoCode
                : fallback;
        }

        private static bool TryParseIsoCode(string isoCode, out Language language)
        {
            if (string.IsNullOrWhiteSpace(isoCode))
            {
                language = Unknown;
                return false;
            }

            language = isoCode.ToLowerInvariant().Trim() switch
            {
                "da" => Danish,
                "en" => English,
                _ => Unknown
            };

            return language != Unknown;
        }
    }
}