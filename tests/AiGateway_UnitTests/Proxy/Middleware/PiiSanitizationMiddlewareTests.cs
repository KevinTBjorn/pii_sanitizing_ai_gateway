using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Proxy.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AiGateway_UnitTests.Proxy.Middleware
{
    public class PiiSanitizationMiddlewareTests
    {
        private readonly Mock<IPiiPayloadSanitizer> _sanitizerMock;
        private readonly NullLogger<PiiSanitizationMiddleware> _logger;

        public PiiSanitizationMiddlewareTests()
        {
            _sanitizerMock = new Mock<IPiiPayloadSanitizer>();
            _logger = NullLogger<PiiSanitizationMiddleware>.Instance;
        }

        [Fact]
        public async Task InvokeAsync_WhenPiiIsFound_ShouldRewriteRequestBodyAndSetContentLength()
        {
            // Arrange
            var originalJson = "{\"message\":\"Mit CPR er 123456-7890\"}";
            var cleanedJson = "{\"message\":\"Mit CPR er [MASKED_DK_CPR]\"}";

            _sanitizerMock
                .Setup(s => s.SanitizePayloadAsync(originalJson, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiiCheckResult(cleanedJson, HasPii: true));

            var context = CreateHttpContext("POST", "application/json", originalJson);

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new PiiSanitizationMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context, _sanitizerMock.Object);

            // Assert
            Assert.True(nextCalled, "Pipeline should delegate to next middleware.");

            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var actualBody = await reader.ReadToEndAsync();

            Assert.Equal(cleanedJson, actualBody);
            Assert.Equal(Encoding.UTF8.GetBytes(cleanedJson).Length, context.Request.ContentLength);
        }

        [Fact]
        public async Task InvokeAsync_WhenNoPiiIsFound_ShouldNotModifyBodyAndCallNext()
        {
            // Arrange
            var originalJson = "{\"message\":\"Hello world\"}";

            _sanitizerMock
                .Setup(s => s.SanitizePayloadAsync(originalJson, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PiiCheckResult(originalJson, HasPii: false));

            var context = CreateHttpContext("POST", "application/json", originalJson);

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new PiiSanitizationMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context, _sanitizerMock.Object);

            // Assert
            Assert.True(nextCalled);

            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var actualBody = await reader.ReadToEndAsync();

            Assert.Equal(originalJson, actualBody);
        }

        [Theory]
        [InlineData("GET", "application/json")]
        [InlineData("DELETE", "application/json")]
        [InlineData("POST", "text/plain")]
        [InlineData("PUT", "text/html")]
        public async Task InvokeAsync_WhenRequestShouldNotBeScanned_ShouldSkipSanitizer(string method, string contentType)
        {
            // Arrange
            var body = "{\"key\":\"value\"}";
            var context = CreateHttpContext(method, contentType, body);

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new PiiSanitizationMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context, _sanitizerMock.Object);

            // Assert
            Assert.True(nextCalled);
            _sanitizerMock.Verify(s => s.SanitizePayloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InvokeAsync_WhenBodyIsEmptyOrWhitespace_ShouldNotInvokeSanitizer()
        {
            // Arrange
            var context = CreateHttpContext("POST", "application/json", "   ");

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new PiiSanitizationMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context, _sanitizerMock.Object);

            // Assert
            Assert.True(nextCalled);
            _sanitizerMock.Verify(s => s.SanitizePayloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InvokeAsync_WhenJsonExceptionThrown_ShouldReturn400AndShortCircuitPipeline()
        {
            // Arrange
            var malformedJson = "{ invalid json }";

            _sanitizerMock
                .Setup(s => s.SanitizePayloadAsync(malformedJson, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new JsonException("Unexpected token"));

            var context = CreateHttpContext("POST", "application/json", malformedJson);
            context.Response.Body = new MemoryStream();

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new PiiSanitizationMiddleware(next, _logger);

            // Act
            await middleware.InvokeAsync(context, _sanitizerMock.Object);

            // Assert
            Assert.False(nextCalled, "Pipeline must short-circuit and NOT call next middleware on bad request.");
            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            Assert.Equal("text/plain", context.Response.ContentType);

            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
            var responseBody = await reader.ReadToEndAsync();

            Assert.Equal("Invalid JSON payload format.", responseBody);
        }

        [Fact]
        public async Task InvokeAsync_WhenSanitizerIsNull_ShouldThrowArgumentNullException()
        {
            // Arrange
            var context = CreateHttpContext("POST", "application/json", "{}");
            RequestDelegate next = (ctx) => Task.CompletedTask;
            var middleware = new PiiSanitizationMiddleware(next, _logger);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => middleware.InvokeAsync(context, null!));
        }

        private static DefaultHttpContext CreateHttpContext(string method, string contentType, string bodyText)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.ContentType = contentType;

            var bytes = Encoding.UTF8.GetBytes(bodyText);
            context.Request.Body = new MemoryStream(bytes);

            return context;
        }
    }
}