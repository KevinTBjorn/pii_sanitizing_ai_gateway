using AiGateway_Core.Constants;
using AiGateway_IntegrationTests.Harness;
using System.Net;
using System.Text;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace AiGateway_IntegrationTests.Middleware
{
    public class YarpPiiGatewayTests : IClassFixture<ProxyTestHarness>
    {
        private readonly ProxyTestHarness _harness;

        public YarpPiiGatewayTests(ProxyTestHarness harness)
        {
            _harness = harness;
            _harness.MockServer.Reset();
        }

        [Fact]
        public async Task Post_WithCprAndEmail_ShouldMaskPiiBeforeForwardingToDownstream()
        {
            // Arrange: Sæt WireMock til at svare 200 OK på /v1/chat/completions
            _harness.MockServer
                .Given(Request.Create().WithPath("/v1/chat/completions").UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBody("""{"choices": [{"message": {"content": "OK"}}]}"""));

            var client = _harness.CreateClient();

            var unmaskedJson = """
        {
          "model": "gpt-4o",
          "messages": [
            {
              "role": "user",
              "content": "Kontakt mig på test.user@domain.dk eller CPR 1205991234."
            }
          ]
        }
        """;

            var content = new StringContent(unmaskedJson, Encoding.UTF8, "application/json");

            // Act: Send request mod vores in-memory YARP proxy
            var response = await client.PostAsync("/v1/chat/completions", content);

            // Assert 1: Proxyen returnerer succesfuldt svar
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert 2: Tjek hvad WireMock (destinationen) reelt modtog
            var logEntries = _harness.MockServer.LogEntries;
            Assert.Single(logEntries);

            var receivedRequestBody = logEntries.First().RequestMessage.Body;

            // Verificer at følsomme oplysninger IKKE er i det videresendte request
            Assert.DoesNotContain("1205991234", receivedRequestBody);
            Assert.DoesNotContain("test.user@domain.dk", receivedRequestBody);

            // Verificer at erstatningsteksterne er til stede
            Assert.Contains(PiiTokens.Cpr, receivedRequestBody);
            Assert.Contains(PiiTokens.Email, receivedRequestBody);
        }
    }
}
