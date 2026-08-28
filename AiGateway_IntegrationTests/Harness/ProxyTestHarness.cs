using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using WireMock.Server;
using Xunit;

namespace AiGateway_IntegrationTests.Harness
{
    public class ProxyTestHarness : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public WireMockServer MockServer { get; private set; } = null!;

        public ValueTask InitializeAsync()
        {
            MockServer = WireMockServer.Start();
            return ValueTask.CompletedTask;
        }

        public override async ValueTask DisposeAsync()
        {
            MockServer.Stop();
            MockServer.Dispose();
            await base.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var overrideSettings = new Dictionary<string, string?>
                {
                    // Matcher præcist: ReverseProxy -> Clusters -> llm-cluster -> Destinations -> llm-destination1 -> Address
                    ["ReverseProxy:Clusters:llm-cluster:Destinations:llm-destination1:Address"] = MockServer.Url
                };
                config.AddInMemoryCollection(overrideSettings);
            });
        }
    }
}
