using AiGateway_Core.Models;
using AiGateway_Core.Options;
using AiGateway_Core.Orchestration;
using AiGateway_Infrastructure.Coordinators;
using AiGateway_Infrastructure.Payloads;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AiGateway_UnitTests.Infrastructure.Payloads
{
    public class JsonPiiPayloadSanitizerTests
    {
        private readonly Mock<IPiiGatewayOrchestrator> _coordinatorMock;
        private readonly HashSet<string> _ignoredKeys;

        public JsonPiiPayloadSanitizerTests()
        {
            _coordinatorMock = new Mock<IPiiGatewayOrchestrator>();
            _ignoredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "apiKey", "model", "ignored_field" };
        }

        [Fact]
        public void Constructor_WhenOptionsValueIsNull_ThrowsArgumentNullException()
        {
            var optionsMock = new Mock<IOptions<PiiGatewayOptions>>();
            optionsMock.Setup(x => x.Value).Returns((PiiGatewayOptions)null!);

            Assert.Throws<ArgumentNullException>(() => new JsonPiiPayloadSanitizer(_coordinatorMock.Object, optionsMock.Object));
        }

        #region Empty / Invalid Input Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SanitizePayloadAsync_WhenBodyIsNullOrEmpty_ReturnsFalseWithoutCallingCoordinator(string? invalidBody)
        {
            // Arrange
            var sanitizer = CreateSanitizer();

            // Act
            var result = await sanitizer.SanitizePayloadAsync(invalidBody!, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.HasPii);
            Assert.Equal(invalidBody, result.CleanedPrompt);
            _coordinatorMock.Verify(x => x.SanitizeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Sanitization Logic Tests

        [Fact]
        public async Task SanitizePayloadAsync_WhenNoPiiDetected_ReturnsHasPiiFalseAndOriginalBody()
        {
            // Arrange
            var json = """{"message": "Hello world", "user": {"name": "John"}}""";

            _coordinatorMock
                .Setup(x => x.SanitizeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string text, CancellationToken _) => new PiiCheckResult(text, false));

            var sanitizer = CreateSanitizer();

            // Act
            var result = await sanitizer.SanitizePayloadAsync(json, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.HasPii);
            Assert.Equal(json, result.CleanedPrompt);
            _coordinatorMock.Verify(x => x.SanitizeTextAsync("Hello world", It.IsAny<CancellationToken>()), Times.Once);
            _coordinatorMock.Verify(x => x.SanitizeTextAsync("John", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SanitizePayloadAsync_WhenPiiIsDetectedInNestedObject_ReplacesValueAndReturnsHasPiiTrue()
        {
            // Arrange
            var json = """{"request": {"prompt": "My email is test@example.com"}}""";

            _coordinatorMock
                .Setup(x => x.SanitizeTextAsync("My email is test@example.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiiCheckResult("My email is [MASKED_EMAIL]", true));

            var sanitizer = CreateSanitizer();

            // Act
            var result = await sanitizer.SanitizePayloadAsync(json, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.HasPii);
            Assert.Contains("[MASKED_EMAIL]", result.CleanedPrompt);

            using var doc = JsonDocument.Parse(result.CleanedPrompt);
            var sanitizedPrompt = doc.RootElement.GetProperty("request").GetProperty("prompt").GetString();
            Assert.Equal("My email is [MASKED_EMAIL]", sanitizedPrompt);
        }

        [Fact]
        public async Task SanitizePayloadAsync_WhenKeyIsIgnored_SkipsSanitizingThatValue()
        {
            // Arrange
            var json = """
            {
                "apiKey": "secret-12345",
                "prompt": "Contact user@test.com"
            }
            """;

            _coordinatorMock
                .Setup(x => x.SanitizeTextAsync("Contact user@test.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiiCheckResult("Contact [MASKED_EMAIL]", true));

            var sanitizer = CreateSanitizer();

            // Act
            var result = await sanitizer.SanitizePayloadAsync(json, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.HasPii);
            _coordinatorMock.Verify(x => x.SanitizeTextAsync("secret-12345", It.IsAny<CancellationToken>()), Times.Never);
            _coordinatorMock.Verify(x => x.SanitizeTextAsync("Contact user@test.com", It.IsAny<CancellationToken>()), Times.Once);

            using var doc = JsonDocument.Parse(result.CleanedPrompt);
            Assert.Equal("secret-12345", doc.RootElement.GetProperty("apiKey").GetString());
            Assert.Equal("Contact [MASKED_EMAIL]", doc.RootElement.GetProperty("prompt").GetString());
        }

        [Fact]
        public async Task SanitizePayloadAsync_WhenArrayIsUnderIgnoredKey_SkipsSanitizingArrayItems()
        {
            // Arrange
            var json = """
            {
                "ignored_field": ["user1@test.com", "user2@test.com"],
                "active_prompt": "Hello"
            }
            """;

            _coordinatorMock
                .Setup(x => x.SanitizeTextAsync("Hello", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiiCheckResult("Hello", false));

            var sanitizer = CreateSanitizer();

            // Act
            var result = await sanitizer.SanitizePayloadAsync(json, TestContext.Current.CancellationToken);

            // Assert
            _coordinatorMock.Verify(x => x.SanitizeTextAsync(It.Is<string>(s => s.Contains("@test.com")), It.IsAny<CancellationToken>()), Times.Never);
            _coordinatorMock.Verify(x => x.SanitizeTextAsync("Hello", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SanitizePayloadAsync_WhenJsonContainsArraysAndMixedTypes_SanitizesOnlyNonIgnoredStrings()
        {
            // Arrange
            var json = """
            {
                "id": 101,
                "is_active": true,
                "metadata": null,
                "messages": ["Normal text", "PII: 123-45-678"]
            }
            """;

            _coordinatorMock
                .Setup(x => x.SanitizeTextAsync("Normal text", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiiCheckResult("Normal text", false));

            _coordinatorMock
                .Setup(x => x.SanitizeTextAsync("PII: 123-45-678", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiiCheckResult("PII: [MASKED_SSN]", true));

            var sanitizer = CreateSanitizer();

            // Act
            var result = await sanitizer.SanitizePayloadAsync(json, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.HasPii);
            _coordinatorMock.Verify(x => x.SanitizeTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            using var doc = JsonDocument.Parse(result.CleanedPrompt);
            var messages = doc.RootElement.GetProperty("messages");
            Assert.Equal("Normal text", messages[0].GetString());
            Assert.Equal("PII: [MASKED_SSN]", messages[1].GetString());
        }

        [Fact]
        public async Task SanitizePayloadAsync_PropagatesCancellationTokenToCoordinator()
        {
            // Arrange
            var json = """{"prompt": "Check token"}""";
            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            _coordinatorMock
                .Setup(x => x.SanitizeTextAsync(It.IsAny<string>(), token))
                .ReturnsAsync(new PiiCheckResult("Check token", false));

            var sanitizer = CreateSanitizer();

            // Act
            await sanitizer.SanitizePayloadAsync(json, token);

            // Assert
            _coordinatorMock.Verify(x => x.SanitizeTextAsync("Check token", token), Times.Once);
        }

        #endregion

        #region Helpers

        private JsonPiiPayloadSanitizer CreateSanitizer()
        {
            var options = Options.Create(new PiiGatewayOptions
            {
                IgnoredKeys = _ignoredKeys
            });

            return new JsonPiiPayloadSanitizer(_coordinatorMock.Object, options);
        }

        #endregion
    }
}