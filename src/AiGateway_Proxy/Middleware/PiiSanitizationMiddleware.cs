using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Coordinators;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using AiGateway_Core.Options;

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

        public async Task InvokeAsync(HttpContext context, IPiiPayloadSanitizer payloadSanitizer)
        {
            ArgumentNullException.ThrowIfNull(payloadSanitizer);

            if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method))
            {
                if (!IsJsonRequest(context.Request.ContentType))
                {
                    _logger.LogWarning("Blocked non-JSON {Method} request to {Path} with Content-Type: {ContentType}",
                        context.Request.Method, context.Request.Path, context.Request.ContentType);

                    context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync("Unsupported Media Type. Only application/json is allowed for POST/PUT requests.", context.RequestAborted);
                    return;
                }

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
                        scanResult = await payloadSanitizer.SanitizePayloadAsync(originalBody, context.RequestAborted);
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

        private static bool IsJsonRequest(string? contentType)
        {
            return contentType != null && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
