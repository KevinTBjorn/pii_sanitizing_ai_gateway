using System.Net;
using System.Net.Http.Json;
using Moq;
using Moq.Protected;
using AiGateway_Core.Interfaces;
using AiGateway_Infrastructure.PiiScanners;

namespace AiGateway_UnitTests.Infrastructure.Scanner
{
    public class PresidioPiiScannerTests
    {
        private readonly Mock<ILanguageDetector> _mockLanguageDetector;

        public PresidioPiiScannerTests()
        {
            _mockLanguageDetector = new Mock<ILanguageDetector>();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task DetectPiiAsync_WhenPromptIsWhitespaceOrNull_ReturnsEmptyListWithoutCallingServices(string? inputPrompt)
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:5001")
            };

            var scanner = new PresidioPiiScanner(httpClient, _mockLanguageDetector.Object);

            // Act
            var result = await scanner.DetectPiiAsync(inputPrompt!, TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result);

            // Verificer at hverken sprogdetektor eller HTTP-klient blev kaldt
            _mockLanguageDetector.Verify(l => l.DetectLanguage(It.IsAny<string>()), Times.Never);
            mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task DetectPiiAsync_WhenPiiIsDetected_ReturnsMappedPiiMatchesCorrectly()
        {
            // Arrange
            var prompt = "Mitt CPR er 123456-7890.";
            var detectedLanguage = "da";

            _mockLanguageDetector
                .Setup(l => l.DetectLanguage(prompt))
                .Returns(detectedLanguage);

            // Forbered JSON respons fra Presidio: {"entity_type": "DK_CPR", "start": 12, "end": 23, "score": 0.95}
            var presidioResponse = new[]
            {
            new
            {
                entity_type = "DK_CPR",
                start = 12,
                end = 23,
                score = 0.95
            }
        };

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().EndsWith("analyze")),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(presidioResponse)
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:5001/")
            };

            var scanner = new PresidioPiiScanner(httpClient, _mockLanguageDetector.Object);

            // Act
            var result = await scanner.DetectPiiAsync(prompt, TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(result);
            var match = result[0];

            Assert.Equal(12, match.Start);
            Assert.Equal(11, match.Length); // 23 - 12 = 11
            Assert.Equal("DK_CPR", match.EntityType);
            Assert.Equal(0.95, match.Score);
            Assert.Equal("123456-7890", match.RawValue); // Substring udtaget fra prompt
        }

        [Fact]
        public async Task DetectPiiAsync_WhenPresidioReturnsHttpError_ThrowsHttpRequestException()
        {
            // Arrange
            var prompt = "Fejlende request";

            _mockLanguageDetector
                .Setup(l => l.DetectLanguage(prompt))
                .Returns("da");

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://localhost:5001/")
            };

            var scanner = new PresidioPiiScanner(httpClient, _mockLanguageDetector.Object);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => scanner.DetectPiiAsync(prompt, TestContext.Current.CancellationToken));
        }
    }
}
