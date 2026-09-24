using AiGateway_Core.PiiScanners;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace AiGateway_IntegrationTests
{
    public class OllamaGatewayIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly WireMockServer _ollamaServer;
        private readonly Mock<IPiiScanner> _piiScannerMock;

        public OllamaGatewayIntegrationTests(WebApplicationFactory<Program> factory)
        {
            // 1. Start WireMock server to simulate local Ollama instance (http://localhost:11434)
            _ollamaServer = WireMockServer.Start();
            _piiScannerMock = new Mock<IPiiScanner>();

            // 2. Configure Gateway pipeline overrides
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    // Redirect YARP cluster to WireMock port dynamically
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ReverseProxy:Clusters:ollama-cluster:Destinations:ollama-instance:Address"] = _ollamaServer.Url
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    // Substitute Presidio HTTP scanner with controlled mock
                    services.AddScoped(_ => _piiScannerMock.Object);
                });
            });
        }

        [Fact]
        public async Task Post_OllamaChatPayload_SanitizesPiiInMessagesAndForwardsToBackend()
        {
            // Arrange
            var client = _factory.CreateClient();
            var rawCpr = "120587-1234";
            var userMessageText = $"Hej, mit navn er Lars Hansen. Mit CPR er {rawCpr}.";

            var requestBody = new
            {
                model = "smollm:135m",
                messages = new[]
                {
            new
            {
                role = "user",
                content = userMessageText
            }
        }
            };

            var cprStart = userMessageText.IndexOf(rawCpr, StringComparison.Ordinal);
            var cprLength = rawCpr.Length;

            _piiScannerMock
                .Setup(s => s.DetectPiiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PiiMatch>
                {
            new PiiMatch(Start: cprStart, Length: cprLength, EntityType: "DK_CPR", Score: 0.99, RawValue: rawCpr)
                });

            // Configure WireMock downstream endpoint
            _ollamaServer
                .Given(Request.Create().WithPath("/api/chat").UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{\"model\":\"smollm:135m\",\"message\":{\"role\":\"assistant\",\"content\":\"Modtaget\"}}"));

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync("/api/chat", jsonContent, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var receivedLogs = _ollamaServer.FindLogEntries(Request.Create().WithPath("/api/chat").UsingPost());
            Assert.NotEmpty(receivedLogs);

            var forwardedBody = receivedLogs[0].RequestMessage?.Body ?? string.Empty;

            Assert.DoesNotContain(rawCpr, forwardedBody);
            Assert.Contains("[MASKED_DK_CPR]", forwardedBody);

            Assert.Contains("smollm:135m", forwardedBody);
            Assert.Contains("user", forwardedBody);
        }

        [Fact]
        public async Task Post_NonJsonPayload_ShortCircuitsWith415()
        {
            // Arrange
            var client = _factory.CreateClient();
            var content = new StringContent("raw text prompt", Encoding.UTF8, "text/plain");

            // Act
            var response = await client.PostAsync("/api/chat", content, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);

            Assert.Empty(_ollamaServer.LogEntries);
        }

        [Fact]
        public async Task Post_MalformedJson_ShortCircuitsWith400()
        {
            // Arrange
            var client = _factory.CreateClient();
            var content = new StringContent("{ \"model\": \"smollm:135m\", invalid json }", Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync("/api/generate", content, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var responseText = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Invalid JSON payload format.", responseText);

            Assert.Empty(_ollamaServer.LogEntries);
        }

        [Fact]
        public async Task Get_PassthroughRoute_ForwardsCleanlyToOllama()
        {
            // Arrange
            var client = _factory.CreateClient();

            _ollamaServer
                .Given(Request.Create().WithPath("/api/tags").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBody("{\"models\":[]}"));

            // Act
            var response = await client.GetAsync("/api/tags", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _piiScannerMock.Verify(s => s.DetectPiiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        public void Dispose()
        {
            _ollamaServer.Stop();
            _ollamaServer.Dispose();
        }
    }
}