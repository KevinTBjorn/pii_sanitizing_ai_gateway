using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.PiiScanners;
using Lingua;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AiGateway_UnitTests.Infrastructure.PiiScanners
{
    public class PresidioPiiScannerTests
    {
        private readonly Mock<ILanguageDetector> _languageDetectorMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

        public PresidioPiiScannerTests()
        {
            _languageDetectorMock = new Mock<ILanguageDetector>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        }

        #region Empty Input

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DetectPiiAsync_WhenPromptIsNullOrEmpty_ReturnsEmptyListAndSkipsHttpCall(string? invalidPrompt)
        {
            // Arrange
            var scanner = CreateScanner();

            // Act
            var result = await scanner.DetectPiiAsync(invalidPrompt!, TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result);
            _languageDetectorMock.Verify(x => x.DetectLanguage(It.IsAny<string>()), Times.Never);
            VerifyHttpSendTimes(Times.Never());
        }

        #endregion

        #region Single Entity Detection & Payload Verification

        [Theory]
        [InlineData("My CPR is 123456-7890", "da", "DK_CPR", 10, 21, 0.95, "123456-7890")]
        [InlineData("Contact user@example.com now", "en", "EMAIL", 8, 24, 1.0, "user@example.com")]
        public async Task DetectPiiAsync_WhenValidPrompt_SendsCorrectPayloadAndParsesMatches(
            string prompt,
            string detectedLanguage,
            string expectedEntityType,
            int expectedStart,
            int expectedEnd,
            double expectedScore,
            string expectedRawValue)
        {
            // Arrange
            _languageDetectorMock.Setup(x => x.DetectLanguage(prompt)).Returns(detectedLanguage);

            var presidioResponse = new[]
            {
                new { entity_type = expectedEntityType, start = expectedStart, end = expectedEnd, score = expectedScore }
            };

            SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(presidioResponse));
            var scanner = CreateScanner();

            // Act
            var results = await scanner.DetectPiiAsync(prompt, TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(results);
            var match = results[0];

            Assert.Equal(expectedEntityType, match.EntityType);
            Assert.Equal(expectedStart, match.Start);
            Assert.Equal(expectedEnd - expectedStart, match.Length);
            Assert.Equal(expectedScore, match.Score);
            Assert.Equal(expectedRawValue, match.RawValue);

            _languageDetectorMock.Verify(x => x.DetectLanguage(prompt), Times.Once);
            VerifyHttpSendTimes(Times.Once());
        }

        #endregion

        #region Overlap Resolution Tests

        [Theory]
        // Case 1: Score = 1.0 takes priority over a higher non-1.0 score
        [InlineData(0, 10, 0.99, 5, 15, 1.0, "5678901234")]
        // Case 2: Longer match takes priority when scores are equal (< 1.0)
        [InlineData(0, 12, 0.90, 2, 8, 0.90, "012345678901")]
        // Case 3: Higher score takes priority when lengths are equal
        [InlineData(0, 10, 0.95, 2, 12, 0.80, "0123456789")]
        public async Task DetectPiiAsync_WhenMatchesOverlap_SelectsWinnerBasedOnPriorityRules(
            int match1Start, int match1End, double match1Score,
            int match2Start, int match2End, double match2Score,
            string expectedRawValue)
        {
            // Arrange
            var prompt = "01234567890123456789";
            _languageDetectorMock.Setup(x => x.DetectLanguage(prompt)).Returns("en");

            var presidioResponse = new[]
            {
                new { entity_type = "TYPE_A", start = match1Start, end = match1End, score = match1Score },
                new { entity_type = "TYPE_B", start = match2Start, end = match2End, score = match2Score }
            };

            SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(presidioResponse));
            var scanner = CreateScanner();

            // Act
            var results = await scanner.DetectPiiAsync(prompt, TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(results);
            Assert.Equal(expectedRawValue, results[0].RawValue);
        }

        #endregion

        #region Error Handling

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task DetectPiiAsync_WhenHttpReturnsErrorCode_ThrowsHttpRequestException(HttpStatusCode statusCode)
        {
            // Arrange
            var prompt = "Valid prompt text";
            _languageDetectorMock.Setup(x => x.DetectLanguage(prompt)).Returns("en");

            SetupHttpResponse(statusCode, "Error message from Presidio");
            var scanner = CreateScanner();

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => scanner.DetectPiiAsync(prompt, TestContext.Current.CancellationToken));
        }

        #endregion

        #region Helper Methods

        private PresidioPiiScanner CreateScanner()
        {
            return new PresidioPiiScanner(CreateHttpClient(), _languageDetectorMock.Object);
        }

        private HttpClient CreateHttpClient()
        {
            return new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:5000/")
            };
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
        }

        private void VerifyHttpSendTimes(Times times)
        {
            _httpMessageHandlerMock
                .Protected()
                .Verify(
                    "SendAsync",
                    times,
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }

        #endregion
    }
}