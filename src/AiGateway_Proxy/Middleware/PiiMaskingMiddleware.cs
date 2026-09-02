using System.Text;
using AiGateway_Core.Interfaces;

namespace AiGateway_Proxy.Middleware
{
    public class PiiMaskingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PiiMaskingMiddleware> _logger;

        public PiiMaskingMiddleware(RequestDelegate next, ILogger<PiiMaskingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IPiiScanner piiScanner)
        {
            // Vi undersøger kun POST/PUT requests med JSON payload (typisk for OpenAI/LLM API'er)
            if (ShouldScanRequest(context.Request))
            {
                // Giver os lov til at læse streamen uden at ødelægge den for YARP
                context.Request.EnableBuffering();

                using var reader = new StreamReader(
                    context.Request.Body,
                    encoding: Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);

                var originalBody = await reader.ReadToEndAsync(context.RequestAborted);
                context.Request.Body.Position = 0; // Spol streamen tilbage til start!

                if (!string.IsNullOrWhiteSpace(originalBody))
                {
                    var scanResult = await piiScanner.ScanAndMaskAsync(originalBody, context.RequestAborted);

                    var maskedBody = scanResult.CleanedPrompt;

                    // Hvis teksten blev ændret, betyder det at der blev fundet PII
                    if (maskedBody != originalBody)
                    {
                        _logger.LogInformation("PII fundet og maskeret i request til: {Path}", context.Request.Path);

                        // Formaterer typerne pænt som en kommasepareret streng i loggen
                        var piiTypesFormatted = string.Join(", ", scanResult.DetectedTypes);
                        _logger.LogInformation("Masked JSON sendt videre: {MaskedJson}, Types: {PiiTypes}", maskedBody, piiTypesFormatted);

                        var bytes = Encoding.UTF8.GetBytes(maskedBody);
                        var modifiedStream = new MemoryStream(bytes);

                        // Udskift den originale request body med vores maskerede version
                        context.Request.Body = modifiedStream;
                        context.Request.ContentLength = bytes.Length; // Vigtigt: Opdater længden, ellers fejler proxyen!
                    }
                }
            }

            // Send requesten videre i kæden (til næste middleware eller afsted via YARP)
            await _next(context);
        }

        private static bool ShouldScanRequest(HttpRequest request)
        {
            var isPostOrPut = request.Method == HttpMethods.Post || request.Method == HttpMethods.Put;
            var isJson = request.ContentType != null && request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);

            return isPostOrPut && isJson;
        }
    }
}
