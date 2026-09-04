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
                // Inicerer et dedikeret "test-setup", som tilføjes direkte i hukommelsen
                var isolatedTestConfig = new Dictionary<string, string?>
                {
                    // 1. Opret en eksplicit test-rute, der fanger alle /v1/ kald
                    ["ReverseProxy:Routes:integration-test-route:ClusterId"] = "integration-test-cluster",
                    ["ReverseProxy:Routes:integration-test-route:Match:Path"] = "/v1/{**catch-all}",

                    // 2. Peg test-klyngen direkte på WireMocks dynamiske port
                    ["ReverseProxy:Clusters:integration-test-cluster:Destinations:mock-destination:Address"] = MockServer.Url
                };

                config.AddInMemoryCollection(isolatedTestConfig);
            });
        }
    }
}
