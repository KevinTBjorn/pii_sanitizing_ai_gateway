using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Proxy.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;

namespace AiGateway_UnitTests.Proxy.Middleware
{
    public class PiiMaskingMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_WhenPiiIsFound_ShouldMaskRequestBodyAndSetContentLength()
        {
            // Arrange
            var mockScanner = new Mock<IPiiScanner>();
            var originalJson = "{\"message\": \"Mit CPR er 123456-7890\"}";
            var maskedJson = "{\"message\": \"Mit CPR er [REDACTED_CPR]\"}";

            // Opsæt scanner-mock til at returnere det maskerede resultat
            mockScanner.Setup(s => s.ScanAndMask(originalJson))
                       .Returns(new PiiScanResult(
                           HasSensitiveData: true,
                           CleanedPrompt: maskedJson,
                           DetectedTypes: new[] { "CPR" }
                       ));

            // Opret HttpContext og fyld request body
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

            var middleware = new PiiMaskingMiddleware(next, NullLogger<PiiMaskingMiddleware>.Instance);

            // Act
            await middleware.InvokeAsync(context, mockScanner.Object);

            // Assert
            Assert.True(nextCalled, "Next delegate skal kaldes for at YARP kan fortsætte");

            // Læs den modificerede request body ud
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var actualBody = await reader.ReadToEndAsync();

            Assert.Equal(maskedJson, actualBody);
            Assert.Equal(Encoding.UTF8.GetBytes(maskedJson).Length, context.Request.ContentLength);
        }

        [Fact]
        public async Task InvokeAsync_WhenNotJsonRequest_ShouldNotScan()
        {
            // Arrange
            var mockScanner = new Mock<IPiiScanner>();
            var context = new DefaultHttpContext();
            context.Request.Method = "GET";

            RequestDelegate next = (ctx) => Task.CompletedTask;
            var middleware = new PiiMaskingMiddleware(next, NullLogger<PiiMaskingMiddleware>.Instance);

            // Act
            await middleware.InvokeAsync(context, mockScanner.Object);

            // Assert
            mockScanner.Verify(s => s.ScanAndMask(It.IsAny<string>()), Times.Never);
        }
    }
}
