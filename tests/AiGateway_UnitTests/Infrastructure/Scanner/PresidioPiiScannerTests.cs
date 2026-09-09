using AiGateway_Core.Constants;
using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Scanners;
using Lingua;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace AiGateway_UnitTests.Infrastructure.Scanner
{
    public class PresidioPiiScannerTests
    {
        private readonly Mock<ILanguageDetector> _mockLanguageDetector;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly PresidioPiiScanner _sut;

        public PresidioPiiScannerTests()
        {
            _mockLanguageDetector = new Mock<ILanguageDetector>();
            _mockLanguageDetector.Setup(x => x.DetectLanguage(It.IsAny<string>())).Returns("da");

            _mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:5001/")
            };

            _sut = new PresidioPiiScanner(_httpClient, _mockLanguageDetector.Object);
        }

        private void SetupPresidioResponse(List<PresidioEntity> entitiesToReturn)
        {
            var jsonResponse = JsonSerializer.Serialize(entitiesToReturn);
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ScanAndMaskAsync_EmptyPrompt_ReturnsOriginalPrompt(string? input)
        {
            // Act
            var result = await _sut.ScanAndMaskAsync(input!, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.HasSensitiveData);
            Assert.Equal(input, result.CleanedPrompt);

            _mockHttpMessageHandler.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task ScanAndMaskAsync_NoEntitiesFound_ReturnsOriginalPrompt()
        {
            // Arrange
            var input = "Dette er en helt almindelig tekst.";
            SetupPresidioResponse(new List<PresidioEntity>());

            // Act
            var result = await _sut.ScanAndMaskAsync(input, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.HasSensitiveData);
            Assert.Equal(input, result.CleanedPrompt);
        }

        [Fact]
        public async Task ScanAndMaskAsync_ValidEntitiesFound_MasksPromptCorrectly()
        {
            // Arrange
            var input = "Mit navn er Sofie Hansen.";
            
            SetupPresidioResponse(new List<PresidioEntity>
            {
                new PresidioEntity { Start = 12, End = 24, EntityType = "PERSON" }
            });

            // Act
            var result = await _sut.ScanAndMaskAsync(input, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.HasSensitiveData);
            Assert.Equal("Mit navn er [PERSON].", result.CleanedPrompt);
            Assert.Contains("PERSON", result.DetectedTypes);
        }

        [Fact]
        public async Task ScanAndMaskAsync_OverlappingPresidioEntities_LongestEntityWins()
        {
            // Arrange
            var input = "Jeg bor i København Kommune.";

            SetupPresidioResponse(new List<PresidioEntity>
            {
                new PresidioEntity { Start = 10, End = 19, EntityType = "CITY" },
                new PresidioEntity { Start = 10, End = 27, EntityType = "LOCATION" }
            });

            // Act
            var result = await _sut.ScanAndMaskAsync(input, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.HasSensitiveData);
            // Forventer at den fjerner hele "København Kommune" fordi det er det længste interval
            Assert.Equal("Jeg bor i [LOCATION].", result.CleanedPrompt);
            Assert.Contains("LOCATION", result.DetectedTypes);
            Assert.DoesNotContain("CITY", result.DetectedTypes);
        }

        [Fact]
        public async Task ScanAndMaskAsync_PresidioOverlapsWithExistingMask_IgnoresPresidioEntity()
        {
            // Arrange
            var input = "Mit cpr er " + PiiTokens.Cpr + " og mit navn er Bo.";

            SetupPresidioResponse(new List<PresidioEntity>
            {
                // Presidio tror fejlagtigt, at en del af MASKED_CPR er en entitet
                new PresidioEntity { Start = 12, End = 18, EntityType = "LOCATION" }, 
                
                // Presidio fanger "Bo" korrekt
                new PresidioEntity { Start = 39, End = 41, EntityType = "PERSON" }
            });

            // Act
            var result = await _sut.ScanAndMaskAsync(input, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.HasSensitiveData);
            // [MASKED_CPR] forbliver uberørt, men "Bo" bliver til [PERSON]
            Assert.Equal("Mit cpr er " + PiiTokens.Cpr + " og mit navn er [PERSON].", result.CleanedPrompt);
        }
    }
}
