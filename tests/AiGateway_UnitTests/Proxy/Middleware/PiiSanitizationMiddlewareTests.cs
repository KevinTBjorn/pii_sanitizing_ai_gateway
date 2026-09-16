using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Coordinators;
using AiGateway_Proxy.Middleware;
using AiGateway_Core.Options;

namespace AiGateway_UnitTests.Proxy.Middleware
{
    public class PiiSanitizationMiddlewareTests
    {
        private readonly IOptions<PiiGatewayOptions> _options;

        public PiiSanitizationMiddlewareTests()
        {
            // Opsæt PiiGatewayOptions med scannable keys til testen
            _options = Options.Create(new PiiGatewayOptions
            {
                IgnoredKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "message", "content", "prompt"
            }
            });
        }

        [Fact]
        public async Task InvokeAsync_WhenPiiIsFound_ShouldMaskRequestBodyAndSetContentLength()
        {
            // Arrange
            var mockScanner = new Mock<IPiiScanner>();
            var mockTransformer = new Mock<IPiiTransformer>();

            var originalText = "Mit CPR er 123456-7890";
            var maskedText = "Mit CPR er [MASKED_DK_CPR]";

            var originalJson = $"{{\"message\": \"{originalText}\"}}";
            var expectedJson = $"{{\"message\":\"{maskedText}\"}}";

            var matches = new List<PiiMatch>
        {
            new PiiMatch(11, 11, "DK_CPR", 0.95, "123456-7890")
        };

            mockScanner
                .Setup(s => s.DetectPiiAsync(originalText, It.IsAny<CancellationToken>()))
                .ReturnsAsync(matches);

            mockTransformer
                .Setup(t => t.PiiTransformAsync(originalText, matches, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiiCheckResult(maskedText, HasPii: true));

            var coordinator = new PiiGatewayCoordinator(mockScanner.Object, mockTransformer.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.ContentType = "application/json";

            var bytes = Encoding.UTF8.GetBytes(originalJson);
            context.Request.Body = new MemoryStream(bytes);

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new PiiSanitizationMiddleware(next, NullLogger<PiiSanitizationMiddleware>.Instance, _options);

            // Act - Send alle 3 parametre med til InvokeAsync
            await middleware.InvokeAsync(context, coordinator, _options);

            // Assert
            Assert.True(nextCalled, "Next delegate skal kaldes for at YARP kan fortsætte");

            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var actualBody = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

            Assert.Equal(expectedJson, actualBody);
            Assert.Equal(Encoding.UTF8.GetBytes(expectedJson).Length, context.Request.ContentLength);
        }

        [Fact]
        public async Task InvokeAsync_WhenNotJsonRequest_ShouldNotScan()
        {
            // Arrange
            var mockScanner = new Mock<IPiiScanner>();
            var mockTransformer = new Mock<IPiiTransformer>();

            var coordinator = new PiiGatewayCoordinator(mockScanner.Object, mockTransformer.Object);

            var context = new DefaultHttpContext();
            context.Request.Method = "GET";

            RequestDelegate next = (ctx) => Task.CompletedTask;

            var middleware = new PiiSanitizationMiddleware(next, NullLogger<PiiSanitizationMiddleware>.Instance, _options);

            // Act - Send alle 3 parametre med til InvokeAsync
            await middleware.InvokeAsync(context, coordinator, _options);

            // Assert
            mockScanner.Verify(s => s.DetectPiiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
