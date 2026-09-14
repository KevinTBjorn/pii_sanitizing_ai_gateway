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
        private readonly HashSet<string> _scannableKeys;

        public PiiSanitizationMiddleware(RequestDelegate next, ILogger<PiiSanitizationMiddleware> logger, IOptions<PiiGatewayOptions> options)
        {
            _next = next;
            _logger = logger;
            _scannableKeys = options.Value.ScannableKeys;
        }

        public async Task InvokeAsync(HttpContext context, PiiGatewayCoordinator coordinator, IOptions<PiiGatewayOptions> options)
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
                        scanResult = await ProcessBodyContentAsync(originalBody, coordinator, options.Value.ScannableKeys, context.RequestAborted);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Ugyldig JSON payload modtaget fra klienten på {Path}", context.Request.Path);

                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "text/plain";
                        await context.Response.WriteAsync("Invalid JSON payload format.", context.RequestAborted);
                        return;
                    }

                    if (scanResult.HasPii)
                    {
                        _logger.LogInformation("PII detekteret og behandlet i request til: {Path}", context.Request.Path);
                        Console.WriteLine($"Cleaned Prompt: {scanResult.CleanedPrompt}");

                        var bytes = Encoding.UTF8.GetBytes(scanResult.CleanedPrompt);
                        context.Request.Body = new MemoryStream(bytes);
                        context.Request.ContentLength = bytes.Length;
                    }
                }
            }

            await _next(context);
        }

        private static async Task<PiiCheckResult> ProcessBodyContentAsync(string rawBody, PiiGatewayCoordinator coordinator, IReadOnlySet<string> scannableKeys, CancellationToken cancellationToken)
        {
            var jsonNode = JsonNode.Parse(rawBody);
            if (jsonNode == null)
                return new PiiCheckResult(rawBody, false);

            bool hasPii = false;

            // Rekursiv funktion der fører forældrenøglen (parentKey) med ned
            async Task ProcessNodeAsync(JsonNode? node, string? parentKey = null)
            {
                if (node is JsonValue val && val.GetValueKind() == JsonValueKind.String)
                {
                    // Tjekker om forældrenøglen (f.eks. "content" eller "prompt") findes i whitelisten
                    if (parentKey != null && scannableKeys.Contains(parentKey))
                    {
                        string originalText = val.GetValue<string>();
                        var result = await coordinator.ProcessPromptAsync(originalText, cancellationToken);

                        if (result.HasPii)
                        {
                            // Erstat direkte i JSON-træet uanset om det ligger i et Objekt eller et Array
                            val.ReplaceWith(JsonValue.Create(result.CleanedPrompt));
                            hasPii = true;
                        }
                    }
                }
                else if (node is JsonObject obj)
                {
                    foreach (var property in obj.ToList())
                    {
                        // Sender feltnavnet (f.eks. "messages" eller "content") med ned som parentKey
                        await ProcessNodeAsync(property.Value, property.Key);
                    }
                }
                else if (node is JsonArray array)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        // Bevarer parentKey for elementerne i et array
                        await ProcessNodeAsync(array[i], parentKey);
                    }
                }
            }

            await ProcessNodeAsync(jsonNode);

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
