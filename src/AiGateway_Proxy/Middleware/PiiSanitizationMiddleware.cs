using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Coordinators;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace AiGateway_Proxy.Middleware
{
    public class PiiSanitizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PiiSanitizationMiddleware> _logger;
        private readonly HashSet<string> _ignoredKeys;

        public PiiSanitizationMiddleware(RequestDelegate next, ILogger<PiiSanitizationMiddleware> logger, IOptions<PiiGatewayOptions> options)
        {
            _next = next;
            _logger = logger;
            _ignoredKeys = options.Value.IgnoredKeys;
        }

        public async Task InvokeAsync(HttpContext context, PiiGatewayCoordinator coordinator)
        {
            if (ShouldScanRequest(context.Request))
            {
                context.Request.EnableBuffering();

                using var reader = new StreamReader(
                    context.Request.Body,
                    encoding: Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);

                var originalBody = await reader.ReadToEndAsync(context.RequestAborted);
                context.Request.Body.Position = 0;

                if (!string.IsNullOrWhiteSpace(originalBody))
                {
                    PiiCheckResult scanResult;

                    try
                    {
                        scanResult = await SanitizeRequestBodyAsync(originalBody, coordinator, _ignoredKeys, context.RequestAborted);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid JSON payload received from client on {Path}", context.Request.Path);

                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "text/plain";
                        await context.Response.WriteAsync("Invalid JSON payload format.", context.RequestAborted);
                        return;
                    }

                    if (scanResult.HasPii)
                    {
                        _logger.LogInformation("PII detected and sanitized in request to: {Path}", context.Request.Path);

                        var bytes = Encoding.UTF8.GetBytes(scanResult.CleanedPrompt);
                        context.Request.Body = new MemoryStream(bytes);
                        context.Request.ContentLength = bytes.Length;
                    }
                }
            }

            await _next(context);
        }

        private static async Task<PiiCheckResult> SanitizeRequestBodyAsync(string rawBody, PiiGatewayCoordinator coordinator, IReadOnlySet<string> ignoredKeys, CancellationToken cancellationToken)
        {
            var jsonNode = JsonNode.Parse(rawBody);
            if (jsonNode == null)
                return new PiiCheckResult(rawBody, false);

            bool hasPii = false;

            async Task InspectNodeAsync(JsonNode? node, string? parentKey = null)
            {
                if (node == null) return;
 
                if (node is JsonValue val && val.GetValueKind() == JsonValueKind.String)
                {
                    if (parentKey != null && ignoredKeys.Contains(parentKey))
                    {
                        return;
                    }

                    string originalText = val.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(originalText))
                    {
                        var result = await coordinator.SanitizeTextAsync(originalText, cancellationToken);

                        if (result.HasPii)
                        {
                            val.ReplaceWith(JsonValue.Create(result.CleanedPrompt));
                            hasPii = true;
                        }
                    }
                }
                else if (node is JsonObject obj)
                {
                    foreach (var property in obj.ToList())
                    {
                        await InspectNodeAsync(property.Value, property.Key);
                    }
                }
                else if (node is JsonArray array)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        await InspectNodeAsync(array[i], parentKey);
                    }
                }
            }

            await InspectNodeAsync(jsonNode);

            return new PiiCheckResult(hasPii ? jsonNode.ToJsonString(UnescapedJsonOptions) : rawBody, hasPii);
        }

        private static bool ShouldScanRequest(HttpRequest request)
        {
            var isPostOrPut = request.Method == HttpMethods.Post || request.Method == HttpMethods.Put;
            var isJson = request.ContentType != null && request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);

            return isPostOrPut && isJson;
        }

        private static readonly JsonSerializerOptions UnescapedJsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
    }
}
