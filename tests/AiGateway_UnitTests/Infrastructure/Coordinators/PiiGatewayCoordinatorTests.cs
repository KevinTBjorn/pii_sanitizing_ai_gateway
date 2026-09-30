using AiGateway_Core.PiiScanners;
using AiGateway_Core.TextMasking;
using AiGateway_Core.Orchestration;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AiGateway_UnitTests.Infrastructure.Coordinators
{
    public class PiiGatewayCoordinatorTests
    {
        private readonly Mock<IPiiScanner> _scannerMock;
        private readonly Mock<IPiiTransformer> _transformerMock;

        public PiiGatewayCoordinatorTests()
        {
            _scannerMock = new Mock<IPiiScanner>();
            _transformerMock = new Mock<IPiiTransformer>();
        }

        #region Empty / Whitespace Input Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SanitizeTextAsync_WhenPromptIsNullOrEmpty_ReturnsFalseAndSkipsDependencies(string? invalidPrompt)
        {
            // Arrange
            var coordinator = CreateCoordinator();

            // Act
            var result = await coordinator.OrchestrateSanitizeProcessAsync(invalidPrompt!, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.HasPii);
            Assert.Equal(invalidPrompt, result.CleanedPrompt);

            _scannerMock.Verify(x => x.DetectPiiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _transformerMock.Verify(x => x.PiiTransformAsync(It.IsAny<string>(), It.IsAny<List<PiiMatch>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Detection & Transformation Logic Tests

        [Fact]
        public async Task SanitizeTextAsync_WhenNoPiiDetected_ReturnsFalseAndDoesNotCallTransformer()
        {
            // Arrange
            var prompt = "Hello, clean prompt!";
            _scannerMock
                .Setup(x => x.DetectPiiAsync(prompt, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PiiMatch>());

            var coordinator = CreateCoordinator();

            // Act
            var result = await coordinator.OrchestrateSanitizeProcessAsync(prompt, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.HasPii);
            Assert.Equal(prompt, result.CleanedPrompt);

            _scannerMock.Verify(x => x.DetectPiiAsync(prompt, It.IsAny<CancellationToken>()), Times.Once);
            _transformerMock.Verify(x => x.PiiTransformAsync(It.IsAny<string>(), It.IsAny<List<PiiMatch>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SanitizeTextAsync_WhenPiiIsDetected_DelegatesToTransformerAndReturnsResult()
        {
            // Arrange
            var prompt = "Contact me at user@example.com";
            var detectedMatches = new List<PiiMatch>
            {
                new(14, 16, "EMAIL", 1.0, "user@example.com")
            };

            var expectedResult = new GatewayExecutionPlan("Contact me at [MASKED_EMAIL]", true);

            _scannerMock
                .Setup(x => x.DetectPiiAsync(prompt, It.IsAny<CancellationToken>()))
                .ReturnsAsync(detectedMatches);

            _transformerMock
                .Setup(x => x.PiiTransformAsync(prompt, detectedMatches, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var coordinator = CreateCoordinator();

            // Act
            var result = await coordinator.OrchestrateSanitizeProcessAsync(prompt, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.HasPii);
            Assert.Equal("Contact me at [MASKED_EMAIL]", result.CleanedPrompt);
        }

        #endregion

        #region Helpers

        private PiiGatewayOrchestrator CreateCoordinator()
        {
            return new PiiGatewayOrchestrator(_scannerMock.Object, _transformerMock.Object, null!);
        }

        #endregion
    }
}