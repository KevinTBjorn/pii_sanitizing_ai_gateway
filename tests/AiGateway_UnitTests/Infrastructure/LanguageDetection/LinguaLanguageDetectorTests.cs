using AiGateway_Core.Options;
using AiGateway_Infrastructure.LanguageDetection;
using Lingua;
using Microsoft.Extensions.Options;

namespace AiGateway_UnitTests.Infrastructure.LanguageDetection
{
    public class LinguaLanguageDetectorTests
    {
        private readonly LinguaLanguageDetector _sut;

        public LinguaLanguageDetectorTests()
        {
            var options = Options.Create(new LanguageDetectionOptions
            {
                SupportedLanguages = new List<string> { "da", "en" },
                DefaultFallback = "en"
            });

            _sut = new LinguaLanguageDetector(options);
        }

        [Theory]
        [InlineData("Dette er en dansk sætning, som skal detekteres som dansk.", "da")]
        [InlineData("Hej, min e-mail er jensen@domain.dk og mit CPR er 1205991234.", "da")]
        [InlineData("Kan du hjælpe mig med at opsætte en reverse proxy i YARP?", "da")]
        [InlineData("This is an English prompt that should be detected as English.", "en")]
        [InlineData("Please scan this text for potential PII and sensitive data.", "en")]
        public void DetectLanguage_ValidInput_ReturnsExpectedLanguageCode(string input, string expectedLanguage)
        {
            // Act
            var result = _sut.DetectLanguage(input);

            // Assert
            Assert.Equal(expectedLanguage, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void DetectLanguage_NullOrWhitespace_ReturnsDefaultFallback(string? input)
        {
            // Act
            var result = _sut.DetectLanguage(input!);

            // Assert
            Assert.Equal("en", result);
        }

        [Theory]
        [InlineData(null, "da", "da")]
        [InlineData("", "fr", "fr")]
        [InlineData("   ", "da", "da")]
        public void DetectLanguage_NullOrWhitespace_ReturnsCustomFallback(string? input, string fallback, string expected)
        {
            // Act
            var result = _sut.DetectLanguage(input!, fallback);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("こんにちは世界")]
        [InlineData("1234567890 !!! ???")]
        [InlineData("Das ist ein deutscher Satz, der nicht in den Modellen enthalten ist.")]
        public void DetectLanguage_UnsupportedLanguage_ReturnsFallback(string input)
        {
            // Act
            var result = _sut.DetectLanguage(input, fallback: "en");

            // Assert
            Assert.Contains(result, new[] { "da", "en" });
        }
    }
}
