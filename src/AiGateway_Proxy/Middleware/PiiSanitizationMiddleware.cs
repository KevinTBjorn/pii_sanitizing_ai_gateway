using AiGateway_Core.Exceptions;
using AiGateway_Core.Options;
using AiGateway_Core.Orchestration;
using AiGateway_Core.RequestParser;
using AiGateway_Core.Routing;
using AiGateway_Infrastructure.RequestParser;
using AiGateway_Infrastructure.Routing;
using Grpc.Net.Client.Balancer;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Options;
using System.Net.Mime;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

namespace AiGateway_Proxy.Middleware
{
    public class PiiSanitizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PiiSanitizationMiddleware> _logger;

        public PiiSanitizationMiddleware(RequestDelegate next, ILogger<PiiSanitizationMiddleware> logger)
        {
            ArgumentNullException.ThrowIfNull(next);
            ArgumentNullException.ThrowIfNull(logger);

            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext, IRequestParser requestParser, IPiiGatewayOrchestrator orchestrator, IGatewayRouter router)
        {
            if (!HttpMethods.IsPost(httpContext.Request.Method))
            {
                await _next(httpContext);
                return;
            }

            httpContext.Request.EnableBuffering();

            try
            {
                RequestPromptContext promptContext = await requestParser.ParseAsync(httpContext.Request, httpContext.RequestAborted);

                RouteDestination destination = router.ResolveRoute(promptContext.PromptRequest.Model)
                    ?? throw new GatewayException(
                        $"The model '{promptContext.PromptRequest.Model}' does not exist or is not configured on this gateway.",
                        StatusCodes.Status404NotFound);

                PiiGatewayExecutionResult result = await orchestrator.OrchestrateSanitizeProcessAsync(promptContext, destination.Format, httpContext.RequestAborted);


                if(result.ContainsPii)
                {
                    _logger.LogWarning("Request payload contains PII. Detected PII types: {PiiTypes}", string.Join(", ", result.DetectedPiiTypes));

                    byte[] newBodyBytes = JsonSerializer.SerializeToUtf8Bytes(result.FormattedPayload);

                    httpContext.Request.Body = new MemoryStream(newBodyBytes);
                    httpContext.Request.ContentLength = newBodyBytes.Length;
                    httpContext.Request.ContentType = "application/json; charset=utf-8";
                }

                await _next(httpContext);
            }
            catch (GatewayException ex)
            {
                _logger.LogWarning(ex, "Gateway request rejected: {Message}", ex.Message);
                await WriteErrorResponseAsync(httpContext, ex.StatusCode, ex.Message);
            }
            catch (BadHttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP framework error: {Message}", ex.Message);
                await WriteErrorResponseAsync(httpContext, ex.StatusCode, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during gateway request execution.");
                await WriteErrorResponseAsync(
                    httpContext,
                    StatusCodes.Status500InternalServerError,
                    "An internal gateway error occurred while processing your request.");
            }
        }

        private static async Task WriteErrorResponseAsync(HttpContext httpContext, int statusCode, string message)
        {
            if (httpContext.Response.HasStarted)
            {
                return;
            }

            httpContext.Response.Clear();
            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json";

            var errorBody = new
            {
                error = new
                {
                    code = statusCode,
                    message = message,
                    timestamp = DateTime.UtcNow
                }
            };

            await JsonSerializer.SerializeAsync(httpContext.Response.Body, errorBody);
        }
    }
}
