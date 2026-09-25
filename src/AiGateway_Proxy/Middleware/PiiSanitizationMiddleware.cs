using AiGateway_Core.Interfaces;
using AiGateway_Core.Options;
using AiGateway_Core.Orchestration;
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
                    PiiCheckResultTemp scanResult;

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


//public async Task InvokeAsync(
//        HttpContext context,
//        IDocumentPromptCoordinator docCoordinator,
//        ITextPromptCoordinator textCoordinator)
//{
//    // 1. TILLAD GENLÆSNING AF HTTP STRØMMEN (Skal ske først!)
//    context.Request.EnableBuffering();

//    try
//    {
//        byte[] transformedPayload = null;

//        if (context.Request.HasFormContentType)
//        {
//            transformedPayload = await docCoordinator.ProcessAsync(context.Request, context.RequestAborted);
//        }
//        else if (context.Request.ContentType?.Contains("application/json") == true)
//        {
//            transformedPayload = await textCoordinator.ProcessAsync(context.Request, context.RequestAborted);
//        }

//        if (transformedPayload != null)
//        {
//            // Overskriv og nulstil strømmen til YARP
//            context.Request.Body = new MemoryStream(transformedPayload);
//            context.Request.ContentLength = transformedPayload.Length;
//            context.Request.ContentType = MediaTypeNames.Application.Json;
//        }

//        await _next(context);
//    }
//    // 2. CENTRAL FEJLMAPPNING TIL HTTP STATUSKODER
//    catch (NotSupportedException ex) // Uunderstøttet filtype fra DocumentExtractionService
//    {
//        await WriteErrorResponseAsync(context, StatusCodes.Status415UnsupportedMediaType, ex.Message);
//    }
//    catch (JsonException ex) // Forkert JSON struktur
//    {
//        await WriteErrorResponseAsync(context, StatusCodes.Status400BadRequest, $"Ugyldigt JSON format: {ex.Message}");
//    }
//    catch (Exception ex) // Alt andet
//    {
//        await WriteErrorResponseAsync(context, StatusCodes.Status500InternalServerError, "En uventet fejl opstod i AI Gateway.");
//    }
//}

//private static async Task WriteErrorResponseAsync(HttpContext context, int statusCode, string message)
//{
//    context.Response.StatusCode = statusCode;
//    context.Response.ContentType = MediaTypeNames.Application.Json;

//    var errorJson = JsonSerializer.Serialize(new
//    {
//        error = message,
//        status = statusCode,
//        timestamp = DateTime.UtcNow
//    });

//    await context.Response.WriteAsync(errorJson);
//}